package orchestrator

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"strings"
	"sync"
	"sync/atomic"
	"testing"
	"time"
	"unicode/utf8"
)

const sampleContent = `{"AppointmentId":"11111111-1111-4111-8111-111111111111"}`

func okAddQueueItem(w http.ResponseWriter, _ capturedRequest) {
	writeJSON(w, http.StatusCreated, `{"Id":42,"Key":"k","Reference":"create-11111111-1111-4111-8111-111111111111","Status":"New"}`)
}

func TestAccessTokenRequestCarriesClientCredentialsAndQueueScope(t *testing.T) {
	f := newFake(t)
	f.onAPI(okAddQueueItem)

	if _, err := f.client().AddQueueItem(context.Background(), "create-x", json.RawMessage(sampleContent)); err != nil {
		t.Fatalf("AddQueueItem: %v", err)
	}

	req := f.tokenRequest()
	if req.Method != http.MethodPost {
		t.Errorf("token method = %s, want POST", req.Method)
	}
	if req.Path != tokenPath {
		t.Errorf("token path = %s, want %s", req.Path, tokenPath)
	}
	if got := req.Header.Get("Content-Type"); got != "application/x-www-form-urlencoded" {
		t.Errorf("token Content-Type = %q, want application/x-www-form-urlencoded", got)
	}
	form, err := url.ParseQuery(req.Body)
	if err != nil {
		t.Fatalf("token body is not a form: %v", err)
	}
	want := map[string]string{
		"grant_type":    "client_credentials",
		"client_id":     "client-id",
		"client_secret": testSecret,
		"scope":         Scope,
	}
	for key, value := range want {
		if got := form.Get(key); got != value {
			t.Errorf("token form %s = %q, want %q", key, got, value)
		}
	}
	if Scope != "OR.Queues" {
		t.Errorf("Scope = %q, want OR.Queues", Scope)
	}
}

func TestAccessTokenIsRequestedOnceAndReused(t *testing.T) {
	f := newFake(t)
	f.onAPI(okAddQueueItem)
	client := f.client()

	for i := 0; i < 2; i++ {
		if _, err := client.AddQueueItem(context.Background(), "create-x", json.RawMessage(sampleContent)); err != nil {
			t.Fatalf("AddQueueItem %d: %v", i+1, err)
		}
	}

	requests, tokenCalls := f.snapshot()
	if tokenCalls != 1 {
		t.Errorf("token requests = %d, want 1", tokenCalls)
	}
	if len(requests) != 3 {
		t.Errorf("requests = %d, want 3 (1 token + 2 AddQueueItem)", len(requests))
	}
}

func TestUnauthorizedDropsTheCachedToken(t *testing.T) {
	f := newFake(t)
	var calls atomic.Int64
	f.onAPI(func(w http.ResponseWriter, _ capturedRequest) {
		if calls.Add(1) == 1 {
			writeJSON(w, http.StatusUnauthorized, `{"message":"token expired"}`)
			return
		}
		okAddQueueItem(w, capturedRequest{})
	})
	client := f.client()

	if _, err := client.AddQueueItem(context.Background(), "create-x", json.RawMessage(sampleContent)); err == nil {
		t.Fatal("AddQueueItem: want an error on HTTP 401, got none")
	}
	if _, err := client.AddQueueItem(context.Background(), "create-x", json.RawMessage(sampleContent)); err != nil {
		t.Fatalf("AddQueueItem after 401: %v", err)
	}

	if _, tokenCalls := f.snapshot(); tokenCalls != 2 {
		t.Errorf("token requests = %d, want 2 (the rejected token is dropped)", tokenCalls)
	}
}

func TestNewClientRefusesAnHTTPClientWithoutTimeout(t *testing.T) {
	f := newFake(t)
	if _, err := NewClient(testConfig(f.server.URL), &http.Client{}); err == nil {
		t.Fatal("NewClient: want an error for a client with no timeout, got none")
	}
	if _, err := NewClient(testConfig(f.server.URL), nil); err == nil {
		t.Fatal("NewClient: want an error for a nil client, got none")
	}
}

func TestErrorsNeverCarryTheSecretOrTheToken(t *testing.T) {
	t.Run("token endpoint echoes the form", func(t *testing.T) {
		f := newFake(t)
		f.onToken(func(w http.ResponseWriter, req capturedRequest) {
			writeJSON(w, http.StatusBadRequest, fmt.Sprintf(`{"error":"invalid_client","sent":%q}`, req.Body))
		})
		_, err := f.client().AddQueueItem(context.Background(), "create-x", json.RawMessage(sampleContent))
		if err == nil {
			t.Fatal("AddQueueItem: want an error, got none")
		}
		assertRedacted(t, "token error", err.Error())
	})

	t.Run("orchestrator echoes the authorization header", func(t *testing.T) {
		f := newFake(t)
		f.onAPI(func(w http.ResponseWriter, req capturedRequest) {
			writeJSON(w, http.StatusInternalServerError,
				fmt.Sprintf(`{"message":"boom","auth":%q,"secret":%q}`, req.Header.Get("Authorization"), testSecret))
		})
		_, err := f.client().AddQueueItem(context.Background(), "create-x", json.RawMessage(sampleContent))
		if err == nil {
			t.Fatal("AddQueueItem: want an error, got none")
		}
		assertRedacted(t, "API error", err.Error())
		var apiErr *APIError
		if !errors.As(err, &apiErr) {
			t.Fatalf("error is %T, want *APIError", err)
		}
		assertRedacted(t, "APIError.Body", apiErr.Body)
	})

	t.Run("the network fails", func(t *testing.T) {
		f := newFake(t)
		client := f.client()
		f.server.Close()
		_, err := client.AddQueueItem(context.Background(), "create-x", json.RawMessage(sampleContent))
		if err == nil {
			t.Fatal("AddQueueItem: want an error against a closed server, got none")
		}
		assertRedacted(t, "transport error", err.Error())
	})

	t.Run("a printed client", func(t *testing.T) {
		f := newFake(t)
		f.onAPI(okAddQueueItem)
		client := f.client()
		if _, err := client.AddQueueItem(context.Background(), "create-x", json.RawMessage(sampleContent)); err != nil {
			t.Fatalf("AddQueueItem: %v", err)
		}
		for _, printed := range []string{
			client.String(),
			client.GoString(),
			fmt.Sprintf("%v", client),
			fmt.Sprintf("%s", client),
			fmt.Sprintf("%#v", client),
		} {
			assertRedacted(t, "printed client", printed)
		}
	})
}

// Finding 1: an Orchestrator error quotes the queue item back, so its text must not
// reach a log through err.Error(). It stays on Body, for -debug to print.
func TestAPIErrorKeepsTheAnswerOutOfItsMessage(t *testing.T) {
	const patient = "POPESCU ION 1850412170013 +40700000001"
	f := newFake(t)
	f.onAPI(func(w http.ResponseWriter, _ capturedRequest) {
		writeJSON(w, http.StatusBadRequest, fmt.Sprintf(`{"message":"invalid item for %s"}`, patient))
	})

	_, err := f.client().AddQueueItem(context.Background(), "create-abc", json.RawMessage(sampleContent))
	if err == nil {
		t.Fatal("AddQueueItem: want an error, got none")
	}
	if strings.Contains(err.Error(), patient) {
		t.Errorf("the error message carries the answer's text: %s", err)
	}
	if !strings.Contains(err.Error(), "400") || !strings.Contains(err.Error(), opAddQueueItem) {
		t.Errorf("error = %q, want it to name the operation and the status", err)
	}
	var apiErr *APIError
	if !errors.As(err, &apiErr) {
		t.Fatalf("error is %T, want *APIError", err)
	}
	if !strings.Contains(apiErr.Body, patient) {
		t.Errorf("APIError.Body = %q, want it to keep the answer for -debug", apiErr.Body)
	}
}

// Finding 2: following a redirect would replay the bearer token, the folder header and
// the queue item onto whatever host the answer named.
func TestRedirectsAreNotFollowed(t *testing.T) {
	const elsewhere = orchestratorPrefix + "elsewhere"
	f := newFake(t)
	var followed atomic.Bool
	f.onAPI(func(w http.ResponseWriter, req capturedRequest) {
		if req.Path == elsewhere {
			followed.Store(true)
			okAddQueueItem(w, req)
			return
		}
		w.Header().Set("Location", elsewhere)
		w.WriteHeader(http.StatusFound)
	})

	_, err := f.client().AddQueueItem(context.Background(), "create-abc", json.RawMessage(sampleContent))
	var apiErr *APIError
	if !errors.As(err, &apiErr) {
		t.Fatalf("error is %T (%v), want *APIError", err, err)
	}
	if apiErr.StatusCode != http.StatusFound {
		t.Errorf("status = %d, want %d", apiErr.StatusCode, http.StatusFound)
	}
	if followed.Load() {
		t.Error("the client followed the redirect and sent the item to the named host")
	}
}

// Finding 5: a token whose lifetime is under twice the refresh margin used to give a
// window already in the past, so every single call asked for a new token.
func TestCacheWindowIsNeverNegative(t *testing.T) {
	cases := []struct {
		expiresIn int64
		want      time.Duration
	}{
		{3600, 3600*time.Second - tokenRefreshMargin},
		{120, 60 * time.Second},
		{60, 30 * time.Second},
		{30, 15 * time.Second},
		{1, 500 * time.Millisecond},
		{999999999, maxTokenLifetime - tokenRefreshMargin},
	}
	for _, c := range cases {
		got := cacheWindow(c.expiresIn)
		if got != c.want {
			t.Errorf("cacheWindow(%d) = %s, want %s", c.expiresIn, got, c.want)
		}
		if got <= 0 {
			t.Errorf("cacheWindow(%d) = %s, want a window in the future", c.expiresIn, got)
		}
	}
}

func TestAShortLivedTokenIsStillReused(t *testing.T) {
	f := newFake(t)
	f.onToken(func(w http.ResponseWriter, _ capturedRequest) { writeToken(w, testToken, 30) })
	f.onAPI(okAddQueueItem)
	client := f.client()

	for i := 0; i < 3; i++ {
		if _, err := client.AddQueueItem(context.Background(), "create-abc", json.RawMessage(sampleContent)); err != nil {
			t.Fatalf("AddQueueItem %d: %v", i+1, err)
		}
	}
	if _, tokenCalls := f.snapshot(); tokenCalls != 1 {
		t.Errorf("token requests = %d, want 1: a 30 second token must still be reused", tokenCalls)
	}
}

func TestAnExpiredTokenIsRequestedAgain(t *testing.T) {
	f := newFake(t)
	f.onAPI(okAddQueueItem)
	client := f.client()
	base := time.Now()
	client.now = func() time.Time { return base }

	if _, err := client.AddQueueItem(context.Background(), "create-abc", json.RawMessage(sampleContent)); err != nil {
		t.Fatalf("AddQueueItem: %v", err)
	}
	client.now = func() time.Time { return base.Add(2 * time.Hour) }
	if _, err := client.AddQueueItem(context.Background(), "create-abc", json.RawMessage(sampleContent)); err != nil {
		t.Fatalf("AddQueueItem after the token expired: %v", err)
	}

	if _, tokenCalls := f.snapshot(); tokenCalls != 2 {
		t.Errorf("token requests = %d, want 2", tokenCalls)
	}
}

// Finding 6: the cache lock is not held across the token request. Concurrent callers
// that start cold still share one request, and none of them fails.
func TestConcurrentCallersShareOneTokenRequest(t *testing.T) {
	f := newFake(t)
	f.onToken(func(w http.ResponseWriter, _ capturedRequest) {
		time.Sleep(20 * time.Millisecond) // hold the fetch, so every caller piles up
		writeToken(w, testToken, 3600)
	})
	f.onAPI(okAddQueueItem)
	client := f.client()

	const callers = 8
	var wg sync.WaitGroup
	errs := make(chan error, callers)
	for i := 0; i < callers; i++ {
		wg.Add(1)
		go func() {
			defer wg.Done()
			_, err := client.AddQueueItem(context.Background(), "create-abc", json.RawMessage(sampleContent))
			errs <- err
		}()
	}
	wg.Wait()
	close(errs)
	for err := range errs {
		if err != nil {
			t.Errorf("AddQueueItem: %v", err)
		}
	}
	if _, tokenCalls := f.snapshot(); tokenCalls != 1 {
		t.Errorf("token requests = %d, want 1 for %d concurrent callers", tokenCalls, callers)
	}
}

func TestCleanTextRemovesControlCharacters(t *testing.T) {
	cases := map[string]string{
		"\x1b[31mred\x1b[0m":  " [31mred [0m",
		"one\r\ntwo":          "one  two",
		"plain text":          "plain text",
		"tab\there":           "tab here",
		"\x1b]0;title\x07end": " ]0;title end",
	}
	for input, want := range cases {
		if got := CleanText(input); got != want {
			t.Errorf("CleanText(%q) = %q, want %q", input, got, want)
		}
	}
	if got := CleanText("\xff\xfe"); !utf8.ValidString(got) {
		t.Errorf("CleanText(%q) = %q, want valid UTF-8 out", "\xff\xfe", got)
	}
}

// A truncated error body used to be reported as if it were the whole answer.
func TestAnIncompleteErrorBodyIsMarked(t *testing.T) {
	f := newFake(t)
	f.onAPI(func(w http.ResponseWriter, _ capturedRequest) {
		// Promise more than is written, then stop: the read fails part way through.
		w.Header().Set("Content-Length", "4096")
		w.WriteHeader(http.StatusInternalServerError)
		io.WriteString(w, `{"message":"the start of an answer`)
	})

	_, err := f.client().AddQueueItem(context.Background(), "create-abc", json.RawMessage(sampleContent))
	var apiErr *APIError
	if !errors.As(err, &apiErr) {
		t.Fatalf("error is %T (%v), want *APIError", err, err)
	}
	if !strings.Contains(apiErr.Body, "incomplete") {
		t.Errorf("Body = %q, want it to say the answer is incomplete", apiErr.Body)
	}
	if !strings.Contains(apiErr.Body, "the start of an answer") {
		t.Errorf("Body = %q, want it to keep the text that did arrive", apiErr.Body)
	}
}

// An error body longer than the 64 KiB cap is read past and thrown away, so the pooled
// connection survives: the transport reuses only a connection read to the end.
func TestALargeErrorBodyKeepsTheConnection(t *testing.T) {
	f := newFake(t)
	f.onAPI(func(w http.ResponseWriter, _ capturedRequest) {
		writeJSON(w, http.StatusInternalServerError,
			`{"message":"`+strings.Repeat("x", maxErrorBodyBytes+40000)+`"}`)
	})
	client := f.client()

	for i := 0; i < 3; i++ {
		if _, err := client.AddQueueItem(context.Background(), "create-abc", json.RawMessage(sampleContent)); err == nil {
			t.Fatalf("AddQueueItem %d: want an error, got none", i+1)
		}
	}
	if got := f.connCount(); got != 1 {
		t.Errorf("the server accepted %d connections, want 1: the failed answers were not drained", got)
	}
}
