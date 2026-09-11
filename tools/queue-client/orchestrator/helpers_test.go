package orchestrator

import (
	"fmt"
	"io"
	"net"
	"net/http"
	"net/http/httptest"
	"net/url"
	"strings"
	"sync"
	"testing"
	"time"
)

// Values every test asserts never reach an error string or any printed form.
const (
	testSecret = "SECRET-do-not-print-3f9a"
	testToken  = "TOKEN-do-not-print-7c1b"
)

const (
	testOrg    = "acme"
	testTenant = "DefaultTenant"

	tokenPath          = "/" + testOrg + "/identity_/connect/token"
	orchestratorPrefix = "/" + testOrg + "/" + testTenant + "/orchestrator_/"
	addQueueItemPath   = orchestratorPrefix + "odata/Queues/UiPathODataSvc.AddQueueItem"
	queueItemsPath     = orchestratorPrefix + "odata/QueueItems"
)

// capturedRequest is what the fake server saw.
type capturedRequest struct {
	Method   string
	Path     string
	RawQuery string
	Query    url.Values
	Header   http.Header
	Body     string
}

func (r capturedRequest) isToken() bool { return strings.HasSuffix(r.Path, "/connect/token") }

// fake is an Orchestrator stand-in. It serves TLS because Config requires https.
type fake struct {
	t      *testing.T
	server *httptest.Server

	mu          sync.Mutex
	requests    []capturedRequest
	tokenCalls  int
	connections int
	token       func(w http.ResponseWriter, req capturedRequest)
	api         func(w http.ResponseWriter, req capturedRequest)
}

func newFake(t *testing.T) *fake {
	t.Helper()
	f := &fake{t: t}
	f.server = httptest.NewUnstartedServer(http.HandlerFunc(f.serve))
	// Counting connections is how a test sees whether a body was drained: the transport
	// reuses only a connection it has read to the end. It has to be set before the
	// server starts, which is why this one is not httptest.NewTLSServer.
	f.server.Config.ConnState = func(_ net.Conn, state http.ConnState) {
		if state == http.StateNew {
			f.mu.Lock()
			f.connections++
			f.mu.Unlock()
		}
	}
	f.server.StartTLS()
	t.Cleanup(f.server.Close)
	// Cleanups run last in, first out, so idle connections go before the server does:
	// closing the server under a live connection logs a TLS handshake error that says
	// nothing about the test.
	t.Cleanup(f.server.Client().CloseIdleConnections)
	return f
}

func (f *fake) serve(w http.ResponseWriter, r *http.Request) {
	body, err := io.ReadAll(io.LimitReader(r.Body, 1<<20))
	if err != nil {
		f.t.Errorf("fake: reading the request body: %v", err)
	}
	req := capturedRequest{
		Method:   r.Method,
		Path:     r.URL.Path,
		RawQuery: r.URL.RawQuery,
		Query:    r.URL.Query(),
		Header:   r.Header.Clone(),
		Body:     string(body),
	}
	f.mu.Lock()
	f.requests = append(f.requests, req)
	handler := f.api
	if req.isToken() {
		f.tokenCalls++
		handler = f.token
	}
	f.mu.Unlock()

	switch {
	case handler != nil:
		handler(w, req)
	case req.isToken():
		writeToken(w, testToken, 3600)
	default:
		writeJSON(w, http.StatusNotFound, `{"message":"the test set no handler for this path"}`)
	}
}

// onToken and onAPI install the answers. Call them before the first request.
func (f *fake) onToken(h func(w http.ResponseWriter, req capturedRequest)) {
	f.mu.Lock()
	defer f.mu.Unlock()
	f.token = h
}

func (f *fake) onAPI(h func(w http.ResponseWriter, req capturedRequest)) {
	f.mu.Lock()
	defer f.mu.Unlock()
	f.api = h
}

func (f *fake) client() *Client {
	f.t.Helper()
	httpClient := f.server.Client()
	httpClient.Timeout = 10 * time.Second
	client, err := NewClient(testConfig(f.server.URL), httpClient)
	if err != nil {
		f.t.Fatalf("NewClient: %v", err)
	}
	return client
}

// connCount is how many TCP connections the server has accepted.
func (f *fake) connCount() int {
	f.mu.Lock()
	defer f.mu.Unlock()
	return f.connections
}

func (f *fake) snapshot() (requests []capturedRequest, tokenCalls int) {
	f.mu.Lock()
	defer f.mu.Unlock()
	requests = make([]capturedRequest, len(f.requests))
	copy(requests, f.requests)
	return requests, f.tokenCalls
}

// tokenRequest returns the only request to the identity endpoint.
func (f *fake) tokenRequest() capturedRequest {
	f.t.Helper()
	return f.only(true)
}

// apiRequest returns the only request to Orchestrator itself.
func (f *fake) apiRequest() capturedRequest {
	f.t.Helper()
	return f.only(false)
}

func (f *fake) only(token bool) capturedRequest {
	f.t.Helper()
	var found []capturedRequest
	requests, _ := f.snapshot()
	for _, r := range requests {
		if r.isToken() == token {
			found = append(found, r)
		}
	}
	if len(found) != 1 {
		f.t.Fatalf("want exactly 1 request (token=%v), got %d of %d", token, len(found), len(requests))
	}
	return found[0]
}

func testConfig(cloudURL string) Config {
	return Config{
		CloudURL:     cloudURL,
		Org:          testOrg,
		Tenant:       testTenant,
		ClientID:     "client-id",
		ClientSecret: testSecret,
		FolderPath:   "PixelData",
		QueueName:    "PixelData_Programari",
	}
}

func writeToken(w http.ResponseWriter, token string, expiresIn int) {
	writeJSON(w, http.StatusOK, fmt.Sprintf(`{"access_token":%q,"expires_in":%d,"token_type":"Bearer"}`, token, expiresIn))
}

func writeJSON(w http.ResponseWriter, status int, body string) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(status)
	io.WriteString(w, body)
}

// assertRedacted fails when s carries the client secret or the access token.
func assertRedacted(t *testing.T, what, s string) {
	t.Helper()
	if strings.Contains(s, testSecret) {
		t.Errorf("%s carries the client secret: %s", what, s)
	}
	if strings.Contains(s, testToken) {
		t.Errorf("%s carries the access token: %s", what, s)
	}
}
