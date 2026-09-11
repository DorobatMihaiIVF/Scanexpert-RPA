package orchestrator

import (
	"bytes"
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"strings"
	"sync"
	"time"
	"unicode"
	"unicode/utf8"
)

const (
	// tokenRefreshMargin: a cached token is replaced this long before it expires.
	tokenRefreshMargin = 60 * time.Second
	// maxTokenLifetime bounds how long any token is cached, whatever expires_in says.
	maxTokenLifetime = 24 * time.Hour
	// maxResponseBytes caps a successful response body. AddQueueItem echoes the item,
	// whose SpecificContent may reach about 512 KB.
	maxResponseBytes = 4 << 20
	// maxErrorBodyBytes caps what is read from a failed response.
	maxErrorBodyBytes = 64 << 10
	// maxErrorBodyInMessage caps the response text kept on an APIError.
	maxErrorBodyInMessage = 1024
	// maxDrainBytes caps what is read and thrown away to let a connection be reused.
	maxDrainBytes = 1 << 20

	redacted = "[redacted]"
)

var (
	// ErrDuplicateReference means the queue already holds an item with this Reference
	// ("Enforce unique references"). Orchestrator's exact status and body for it are
	// (de verificat): any non-2xx AddQueueItem answer whose body contains
	// "duplicate reference", case-insensitive, is classified as this error.
	ErrDuplicateReference = errors.New("duplicate reference")
	// ErrNotFound means no queue item carries the requested Reference.
	ErrNotFound = errors.New("queue item not found")
)

// APIError is a non-2xx answer from the identity server or Orchestrator.
type APIError struct {
	Op         string
	StatusCode int
	// Body is the answer's text with the client secret and the access token replaced,
	// cleaned of control characters and truncated. NOTHING ELSE is taken out of it, and
	// an Orchestrator error quotes the queue item back, so it can carry the patient's
	// name, CNP and phone number. That is why Error() leaves it out: a caller that logs
	// the error logs no patient data, and one that prints Body chose to.
	Body string
	kind error
}

// Error names the operation and the status, never the answer's text. See Body.
func (e *APIError) Error() string {
	return fmt.Sprintf("orchestrator: %s: HTTP %d", e.Op, e.StatusCode)
}

// Unwrap lets errors.Is match ErrDuplicateReference.
func (e *APIError) Unwrap() error { return e.kind }

// transportError carries a redacted message and still unwraps to the cause, so a
// caller can test for context.DeadlineExceeded.
type transportError struct {
	msg string
	err error
}

func (e *transportError) Error() string { return e.msg }
func (e *transportError) Unwrap() error { return e.err }

// Client talks to one Orchestrator folder and queue. It is safe for concurrent use.
// The access token is kept in memory only, never on disk.
type Client struct {
	cfg  Config
	http *http.Client
	now  func() time.Time

	// fetchMu admits one token request at a time. It is never mu: holding the cache
	// lock across an HTTP request would block every other caller, including one that
	// only wanted to read a token that is already there.
	fetchMu sync.Mutex

	mu         sync.Mutex
	token      string
	validUntil time.Time
}

// NewClient validates cfg and returns a client. httpClient must have a Timeout.
func NewClient(cfg Config, httpClient *http.Client) (*Client, error) {
	if err := cfg.Validate(); err != nil {
		return nil, err
	}
	if httpClient == nil || httpClient.Timeout <= 0 {
		return nil, errors.New("orchestrator: the http client must have a timeout")
	}
	// A copy, so the caller's own client keeps its behaviour. Orchestrator answers
	// these two calls directly, and following a redirect would replay the bearer
	// token, the folder header and the queue item onto whatever host an answer named:
	// a 3xx is therefore returned as it is and becomes an APIError.
	direct := *httpClient
	direct.CheckRedirect = func(*http.Request, []*http.Request) error {
		return http.ErrUseLastResponse
	}
	return &Client{cfg: cfg, http: &direct, now: time.Now}, nil
}

// String keeps the cached token and the secret out of any printed Client.
func (c *Client) String() string { return "orchestrator.Client{" + c.cfg.String() + "}" }

// GoString keeps %#v from printing the cached token.
func (c *Client) GoString() string { return c.String() }

// accessToken returns the cached token, or requests a new one when there is none or
// the cached one is within tokenRefreshMargin of expiry. Callers that arrive with no
// token queue on fetchMu and the first one's token serves all of them.
func (c *Client) accessToken(ctx context.Context) (string, error) {
	if token, ok := c.cachedToken(); ok {
		return token, nil
	}
	c.fetchMu.Lock()
	defer c.fetchMu.Unlock()
	// Someone else may have fetched one while this caller waited for fetchMu.
	if token, ok := c.cachedToken(); ok {
		return token, nil
	}
	token, validUntil, err := c.requestToken(ctx)
	if err != nil {
		return "", err
	}
	c.mu.Lock()
	c.token, c.validUntil = token, validUntil
	c.mu.Unlock()
	return token, nil
}

// cachedToken returns the cached token while it is still worth using.
func (c *Client) cachedToken() (string, bool) {
	c.mu.Lock()
	defer c.mu.Unlock()
	if c.token != "" && c.now().Before(c.validUntil) {
		return c.token, true
	}
	return "", false
}

// requestToken asks the identity server for a token and says how long to keep it.
func (c *Client) requestToken(ctx context.Context) (string, time.Time, error) {
	form := url.Values{
		"grant_type":    {"client_credentials"},
		"client_id":     {c.cfg.ClientID},
		"client_secret": {c.cfg.ClientSecret},
		"scope":         {Scope},
	}
	req, err := http.NewRequestWithContext(ctx, http.MethodPost, c.cfg.tokenURL(), strings.NewReader(form.Encode()))
	if err != nil {
		return "", time.Time{}, c.wrap("token", err, "")
	}
	req.Header.Set("Content-Type", "application/x-www-form-urlencoded")
	req.Header.Set("Accept", "application/json")
	body, err := c.send(req, "token", "")
	if err != nil {
		return "", time.Time{}, err
	}
	var answer struct {
		AccessToken string `json:"access_token"`
		ExpiresIn   int64  `json:"expires_in"`
	}
	if err := json.Unmarshal(body, &answer); err != nil {
		return "", time.Time{}, errors.New("orchestrator: token: response is not the expected JSON")
	}
	if answer.AccessToken == "" || answer.ExpiresIn <= 0 {
		return "", time.Time{}, errors.New("orchestrator: token: response has no access_token or expires_in")
	}
	return answer.AccessToken, c.now().Add(cacheWindow(answer.ExpiresIn)), nil
}

// cacheWindow is how long a token whose expires_in is expiresIn seconds is kept.
// It is the lifetime less the refresh margin, but never less than half the lifetime:
// a token shorter than twice the margin would otherwise give a window already in the
// past, and every single call would then ask the identity server for a new token.
func cacheWindow(expiresIn int64) time.Duration {
	lifetime := maxTokenLifetime
	if expiresIn < int64(maxTokenLifetime/time.Second) {
		lifetime = time.Duration(expiresIn) * time.Second
	}
	window := lifetime - tokenRefreshMargin
	if half := lifetime / 2; window < half {
		window = half
	}
	return window
}

// forgetToken drops token from the cache after Orchestrator rejected it.
func (c *Client) forgetToken(token string) {
	c.mu.Lock()
	defer c.mu.Unlock()
	if c.token == token {
		c.token, c.validUntil = "", time.Time{}
	}
}

// call sends one authenticated request to {cloud}/{org}/{tenant}/orchestrator_/<path>.
func (c *Client) call(ctx context.Context, op, method, path string, payload []byte) ([]byte, error) {
	token, err := c.accessToken(ctx)
	if err != nil {
		return nil, err
	}
	var body io.Reader
	if payload != nil {
		body = bytes.NewReader(payload)
	}
	req, err := http.NewRequestWithContext(ctx, method, c.cfg.orchestratorBase()+path, body)
	if err != nil {
		return nil, c.wrap(op, err, token)
	}
	req.Header.Set("Authorization", "Bearer "+token)
	req.Header.Set("Accept", "application/json")
	req.Header.Set("X-UIPATH-FolderPath", c.cfg.FolderPath)
	if payload != nil {
		req.Header.Set("Content-Type", "application/json")
	}
	answer, err := c.send(req, op, token)
	var apiErr *APIError
	if errors.As(err, &apiErr) && apiErr.StatusCode == http.StatusUnauthorized {
		c.forgetToken(token)
	}
	return answer, err
}

// send performs req and returns the body of a 2xx answer; any other status becomes an
// *APIError. token is the bearer token in use ("" for the token request itself).
func (c *Client) send(req *http.Request, op, token string) ([]byte, error) {
	resp, err := c.http.Do(req)
	if err != nil {
		return nil, c.wrap(op, err, token)
	}
	// Drain before closing, so a body this function stopped reading early does not cost
	// the pooled connection: the transport reuses only a connection whose body it has
	// read to the end. The drain is itself bounded, or an endless error body would hold
	// this here for as long as the server kept writing.
	defer func() {
		io.Copy(io.Discard, io.LimitReader(resp.Body, maxDrainBytes))
		resp.Body.Close()
	}()
	if resp.StatusCode < 200 || resp.StatusCode > 299 {
		raw, readErr := io.ReadAll(io.LimitReader(resp.Body, maxErrorBodyBytes))
		return nil, c.apiError(op, resp.StatusCode, raw, token, readErr)
	}
	body, err := io.ReadAll(io.LimitReader(resp.Body, maxResponseBytes+1))
	if err != nil {
		return nil, c.wrap(op, err, token)
	}
	if len(body) > maxResponseBytes {
		return nil, fmt.Errorf("orchestrator: %s: response larger than %d bytes", op, maxResponseBytes)
	}
	return body, nil
}

func (c *Client) wrap(op string, err error, token string) error {
	return &transportError{msg: c.redact(fmt.Sprintf("orchestrator: %s: %v", op, err), token), err: err}
}

// apiError builds the error for a non-2xx answer. readErr is whatever stopped the body
// being read; the text that did arrive is kept, and marked as partial rather than being
// passed off as the whole answer.
func (c *Client) apiError(op string, status int, raw []byte, token string, readErr error) *APIError {
	text := CleanText(c.redact(strings.TrimSpace(string(raw)), token))
	e := &APIError{Op: op, StatusCode: status, Body: truncate(text, maxErrorBodyInMessage)}
	if op == opAddQueueItem && strings.Contains(strings.ToLower(text), "duplicate reference") {
		e.kind = ErrDuplicateReference
	}
	if readErr != nil {
		e.Body = strings.TrimSpace(e.Body + " ...(the answer stopped early and is incomplete)")
	}
	return e
}

func (c *Client) redact(s, token string) string {
	if c.cfg.ClientSecret != "" {
		s = strings.ReplaceAll(s, c.cfg.ClientSecret, redacted)
	}
	if token != "" {
		s = strings.ReplaceAll(s, token, redacted)
	}
	return s
}

// CleanText makes text that arrived from a file or from the server safe to print on
// one terminal line: every control character becomes a space, so no escape sequence
// can repaint the operator's terminal, and invalid UTF-8 is replaced. Run it over any
// value the caller did not write itself before printing it.
func CleanText(s string) string {
	return strings.Map(func(r rune) rune {
		if unicode.IsControl(r) {
			return ' '
		}
		return r
	}, strings.ToValidUTF8(s, "�"))
}

func truncate(s string, n int) string {
	if len(s) <= n {
		return s
	}
	cut := n
	for cut > 0 && !utf8.RuneStart(s[cut]) {
		cut--
	}
	return s[:cut] + "...(truncated)"
}
