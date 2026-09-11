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

// Orchestrator's documented custom error codes, from
// https://docs.uipath.com/orchestrator/automation-cloud/latest/api-guide/response-codes
//
// Only codes whose documented meaning actually covers this client's two calls are here.
// Two that read as if they would fit are deliberately absent, because the page scopes
// each to an unrelated endpoint: 1017 ForbiddenOperation is package and library
// DOWNLOAD only despite its general name, and 1102 OrganizationUnitNotEditable is one
// user endpoint. Reusing either for a queue failure would be inventing a meaning the
// documentation does not give.
const (
	// errorCodeItemNotFound: 1002 ItemNotFound, "a request to a resource that does not
	// exist in the database ... returned for tenants, assets, jobs, host licenses,
	// queues and queue items, processes, settings, and users". Queues are named, but so
	// are six other things, which is why the hint names the setting rather than
	// asserting the queue is the missing one.
	errorCodeItemNotFound = 1002
	// errorCodeDuplicateReference: 1016 DuplicateReference, "Error creating
	// [ReferenceName]. Duplicate Reference."
	errorCodeDuplicateReference = 1016
	// errorCodeInvalidOrganizationUnit: 1100 InvalidOrganizationUnit, "trying to make
	// calls using a user that is associated to a different organization unit than the
	// one you are trying to access".
	errorCodeInvalidOrganizationUnit = 1100
	// errorCodeRequiredOrganizationUnit: 1101 RequiredOrganizationUnit, "thrown when
	// making POST requests endpoint without including an organization unit as a
	// parameter". This client always sends the folder header, so this code means the
	// header did not arrive: a defect here, not a wrong setting.
	errorCodeRequiredOrganizationUnit = 1101
	// errorCodeTransactionReferenceRequired: 1850 TransactionReferenceRequired. The
	// response-codes page gives the name with NO description; the message comes from
	// https://docs.uipath.com/orchestrator/automation-cloud/latest/api-guide/transactions-requests
	// — "Error creating Transaction. Reference is required for Unique Reference
	// Queues." This client always sends a Reference, so this too is a defect here.
	errorCodeTransactionReferenceRequired = 1850
)

// duplicateReferenceText is the fallback signal, matched case-insensitively.
const duplicateReferenceText = "duplicate reference"

// The hints. Every one is written HERE, never taken from the answer, so printing one
// cannot leak the queue item back. Each names the setting to look at, or says plainly
// that the fault is this tool's.
const (
	hintItemNotFound = "check UIPATH_QUEUE_NAME: the queue must already exist in the folder named by UIPATH_FOLDER_PATH. " +
		"Orchestrator returns this same code for several other missing resources, so it is not proof the queue is the missing one."
	hintInvalidOrganizationUnit  = "check UIPATH_FOLDER_PATH: the application is associated with a different folder than the one this call asked for."
	hintRequiredOrganizationUnit = "Orchestrator received no folder, although this client always sends one. " +
		"That is a defect in queue-client rather than a setting you can change: report it."
	hintTransactionReferenceRequired = "the queue wants a Reference and this request carried none, although this client always sends one. " +
		"That is a defect in queue-client rather than a setting you can change: report it."
	// hintForbidden answers a bare 403. See classifyAnswer: this one mapping is
	// INFERRED, not documented.
	hintForbidden = "Orchestrator refused the folder. Check UIPATH_FOLDER_PATH, and that the application has the " +
		"Queues.View and Transactions.Create permissions in that folder."
)

var (
	// ErrDuplicateReference means the queue already holds an item with this Reference
	// ("Enforce unique references"). It is recognised from the documented error code
	// 1016 DuplicateReference in the answer's body, with the body text as a fallback;
	// the HTTP status is never the trigger and is still (de verificat). See
	// classifyAnswer.
	ErrDuplicateReference = errors.New("duplicate reference")
	// ErrNotFound means no queue item carries the requested Reference.
	ErrNotFound = errors.New("queue item not found")
	// ErrConfiguration means Orchestrator refused the call over how this client is
	// configured or how it built the request — the wrong queue, the wrong folder, no
	// folder, no Reference — rather than over anything about the appointment. Retrying
	// it unchanged cannot help, so the caller stops and points at the setting instead.
	// The APIError carries the text to show in Hint.
	ErrConfiguration = errors.New("orchestrator refused this configuration")
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
	// Hint is this client's OWN sentence about a recognised failure, naming the setting
	// to look at. Unlike Body it is a constant from this file and never holds a byte the
	// server sent, so it is safe to print and to log without -debug.
	Hint string
	kind error
}

// Error names the operation and the status, never the answer's text. See Body.
func (e *APIError) Error() string {
	return fmt.Sprintf("orchestrator: %s: HTTP %d", e.Op, e.StatusCode)
}

// Unwrap lets errors.Is match ErrDuplicateReference and ErrConfiguration.
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
	e.kind, e.Hint = classifyAnswer(op, status, raw, text)
	if readErr != nil {
		e.Body = strings.TrimSpace(e.Body + " ...(the answer stopped early and is incomplete)")
	}
	return e
}

// errorResponse is the part of an Orchestrator error answer the client reads. Only the
// error code is taken from it; nothing here is kept or printed, so the rest of the
// answer stays on APIError.Body, which -debug alone prints.
//
// THE FIELD NAME IS OBSERVED, NOT DOCUMENTED. The response-codes page documents the
// CODES and their meanings, but it documents no error body at all: it shows no JSON
// example and never names an "errorCode" field. So the codes below are a contract and
// this envelope is not (de verificat) — which is why classifyAnswer keeps a fallback
// that works when the field is absent, and why nothing here is fatal when it is.
type errorResponse struct {
	ErrorCode json.Number `json:"errorCode"`
}

// errorCodeOf reads Orchestrator's custom error code out of a failed answer. It reports
// false when the answer is not JSON, or carries no code, or carries one that is not a
// number — each of which leaves the caller on its fallback rather than on a guess.
//
// raw is the answer as it arrived, because the redacted, cleaned and truncated form is
// not JSON any more.
func errorCodeOf(raw []byte) (int64, bool) {
	var answer errorResponse
	if err := json.Unmarshal(raw, &answer); err != nil {
		return 0, false
	}
	code, err := answer.ErrorCode.Int64()
	if err != nil {
		return 0, false
	}
	return code, true
}

// classifyAnswer says what a non-2xx answer means and what to tell the operator: the
// error class the caller matches with errors.Is, and this client's own hint, "" when
// there is nothing useful to add.
//
// THE ERROR CODE IS THE SIGNAL, NOT THE HTTP STATUS. The status Orchestrator answers
// each of these with is not documented (de verificat), so keying on one would both
// misread unrelated failures carrying that status and miss the real thing answered with
// another. An answer that names its code is believed: a recognised code decides, and an
// unrecognised one is another failure whatever its message says.
//
// Two fallbacks run only when no code was readable, and both are marked where they are:
// the duplicate text match, and the bare 403.
func classifyAnswer(op string, status int, raw []byte, text string) (error, string) {
	if code, ok := errorCodeOf(raw); ok {
		switch code {
		case errorCodeDuplicateReference:
			// Guarded by operation: this envelope is Orchestrator's, and a read
			// answering 1016 is not a duplicate enqueue.
			if op == opAddQueueItem {
				return ErrDuplicateReference, ""
			}
		case errorCodeItemNotFound:
			return ErrConfiguration, hintItemNotFound
		case errorCodeInvalidOrganizationUnit:
			return ErrConfiguration, hintInvalidOrganizationUnit
		case errorCodeRequiredOrganizationUnit:
			return ErrConfiguration, hintRequiredOrganizationUnit
		case errorCodeTransactionReferenceRequired:
			return ErrConfiguration, hintTransactionReferenceRequired
		}
		// A recognised code that did not match, or one this client does not know: fall
		// through, so a 403 carrying it still reaches the mapping below.
	} else if op == opAddQueueItem && strings.Contains(strings.ToLower(text), duplicateReferenceText) {
		// No code to read, so the message is all there is. Without this a missing field
		// would turn a duplicate into a hard failure and report an enqueue as broken
		// while the appointment is already in the queue.
		return ErrDuplicateReference, ""
	}

	// INFERRED, NOT DOCUMENTED: no official page maps a status to this endpoint's
	// permission failure, and there is no documented code at all for "the caller lacks
	// Queues.View or Transactions.Create in this folder". A 403 that named no code we
	// know is therefore read as a refused folder — the one status-based mapping here,
	// and it is a hint plus an exit code, never a claim about which code Orchestrator
	// meant. The docs also do not say whether a wrong folder answers not-found or
	// not-authorised, so nothing below branches as if they did.
	if status == http.StatusForbidden {
		return ErrConfiguration, hintForbidden
	}
	return nil, ""
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
