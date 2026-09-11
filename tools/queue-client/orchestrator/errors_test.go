package orchestrator

// How a failed answer is classified: which failures get their own error class, which
// carry a hint naming the setting to fix, and which stay a plain exit-1 API error.
// The code under test is classifyAnswer and errorCodeOf in client.go.

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"net/http"
	"strings"
	"testing"
)

func TestAddQueueItemReportsADuplicateReference(t *testing.T) {
	// An answer carrying no error code falls back to the text of its message. The
	// status is (de verificat) and is not what classifies any of these.
	bodies := map[string]string{
		"Duplicate Reference": `{"message":"Error creating Transaction. Duplicate Reference."}`,
		"duplicate reference": `{"message":"duplicate reference"}`,
	}
	for name, body := range bodies {
		t.Run(name, func(t *testing.T) {
			f := newFake(t)
			f.onAPI(func(w http.ResponseWriter, _ capturedRequest) {
				writeJSON(w, http.StatusConflict, body)
			})
			_, err := f.client().AddQueueItem(context.Background(), "create-abc", json.RawMessage(sampleContent))
			if !errors.Is(err, ErrDuplicateReference) {
				t.Fatalf("error = %v, want ErrDuplicateReference", err)
			}
		})
	}

	t.Run("another failure is not a duplicate", func(t *testing.T) {
		f := newFake(t)
		f.onAPI(func(w http.ResponseWriter, _ capturedRequest) {
			writeJSON(w, http.StatusForbidden, `{"message":"no permission on this folder"}`)
		})
		_, err := f.client().AddQueueItem(context.Background(), "create-abc", json.RawMessage(sampleContent))
		if err == nil {
			t.Fatal("AddQueueItem: want an error, got none")
		}
		if errors.Is(err, ErrDuplicateReference) {
			t.Fatalf("error = %v, want something other than ErrDuplicateReference", err)
		}
	})
}

// The documented signal is error code 1016 DuplicateReference, and it is read whatever
// the HTTP status is: the status for a duplicate is not documented (de verificat), so
// nothing here may depend on it. Every message below deliberately avoids the words
// "duplicate reference", which is what makes the error code the only thing that can
// have classified these answers.
func TestADuplicateIsRecognisedFromTheErrorCodeOnAnyStatus(t *testing.T) {
	bodies := map[string]string{
		"number":           `{"message":"Error creating Transaction.","errorCode":1016,"resourceIds":null}`,
		"quoted number":    `{"message":"Error creating Transaction.","errorCode":"1016"}`,
		"code only":        `{"errorCode":1016}`,
		"unexpected field": `{"errorCode":1016,"traceId":"abc","details":{"attempt":2}}`,
	}
	statuses := []int{http.StatusBadRequest, http.StatusConflict, http.StatusInternalServerError}
	for name, body := range bodies {
		for _, status := range statuses {
			t.Run(fmt.Sprintf("%s/%d", name, status), func(t *testing.T) {
				f := newFake(t)
				f.onAPI(func(w http.ResponseWriter, _ capturedRequest) {
					writeJSON(w, status, body)
				})
				_, err := f.client().AddQueueItem(context.Background(), "create-abc", json.RawMessage(sampleContent))
				if !errors.Is(err, ErrDuplicateReference) {
					t.Fatalf("error = %v, want ErrDuplicateReference for HTTP %d", err, status)
				}
			})
		}
	}
}

// An answer that is not JSON, or JSON without an error code, still has to be read: a
// missing field must not turn a duplicate into a hard failure and report an enqueue as
// broken when the appointment is already in the queue.
func TestADuplicateIsRecognisedFromTheTextWhenThereIsNoErrorCode(t *testing.T) {
	bodies := map[string]string{
		"plain text":     "Error creating Transaction. Duplicate Reference.",
		"HTML":           "<html><body>Duplicate Reference</body></html>",
		"truncated JSON": `{"message":"Error creating Transaction. Duplicate Reference.`,
		"JSON, no code":  `{"message":"Error creating Transaction. Duplicate Reference."}`,
		"code is a word": `{"message":"Duplicate Reference.","errorCode":"unknown"}`,
	}
	for name, body := range bodies {
		t.Run(name, func(t *testing.T) {
			f := newFake(t)
			f.onAPI(func(w http.ResponseWriter, _ capturedRequest) {
				writeJSON(w, http.StatusConflict, body)
			})
			_, err := f.client().AddQueueItem(context.Background(), "create-abc", json.RawMessage(sampleContent))
			if !errors.Is(err, ErrDuplicateReference) {
				t.Fatalf("error = %v, want ErrDuplicateReference", err)
			}
		})
	}
}

// Another documented error code is another failure, and must reach the caller as one:
// treating it as a duplicate would exit 0 and report an appointment as queued when
// nothing was queued.
func TestAnotherErrorCodeIsNotADuplicate(t *testing.T) {
	bodies := map[string]string{
		"queue not found": `{"message":"Queue not found.","errorCode":1002}`,
		"no permission":   `{"message":"Access to the path is denied.","errorCode":0}`,
		"code beats text": `{"message":"Error creating Transaction. Duplicate Reference.","errorCode":1002}`,
		"negative is not": `{"message":"boom","errorCode":-1016}`,
	}
	statuses := []int{http.StatusBadRequest, http.StatusConflict, http.StatusForbidden}
	for name, body := range bodies {
		for _, status := range statuses {
			t.Run(fmt.Sprintf("%s/%d", name, status), func(t *testing.T) {
				f := newFake(t)
				f.onAPI(func(w http.ResponseWriter, _ capturedRequest) {
					writeJSON(w, status, body)
				})
				_, err := f.client().AddQueueItem(context.Background(), "create-abc", json.RawMessage(sampleContent))
				if err == nil {
					t.Fatalf("AddQueueItem: want an error for HTTP %d, got none", status)
				}
				if errors.Is(err, ErrDuplicateReference) {
					t.Fatalf("error = %v, want something other than ErrDuplicateReference", err)
				}
			})
		}
	}
}

// The error code classifies AddQueueItem alone. A read answering 1016 is not a
// duplicate enqueue and must not be silently turned into one.
func TestTheDuplicateErrorCodeIsReadOnlyForAddQueueItem(t *testing.T) {
	f := newFake(t)
	f.onAPI(func(w http.ResponseWriter, _ capturedRequest) {
		writeJSON(w, http.StatusConflict, `{"message":"Duplicate Reference.","errorCode":1016}`)
	})
	_, err := f.client().LatestQueueItemByReference(context.Background(), "create-abc")
	if err == nil {
		t.Fatal("LatestQueueItemByReference: want an error, got none")
	}
	if errors.Is(err, ErrDuplicateReference) {
		t.Fatalf("error = %v, want something other than ErrDuplicateReference", err)
	}
}

// The four documented codes that mean this client is misconfigured, or built the request
// wrongly. Each carries a hint written in this package rather than taken from the answer,
// and each is exit 2 at the CLI. The code decides, never the status: every case below is
// asserted on three different ones, only one of which is 403.
func TestConfigurationErrorCodesAreRecognised(t *testing.T) {
	cases := map[string]struct {
		body string
		hint string
		// names is what the operator has to be told to look at, "" when the failure is
		// this tool's own defect and there is no setting to change.
		names string
	}{
		"1002 the queue is not in that folder": {
			body:  `{"message":"Queue not found.","errorCode":1002}`,
			hint:  hintItemNotFound,
			names: "UIPATH_QUEUE_NAME",
		},
		"1100 the application belongs to another folder": {
			body:  `{"message":"Invalid organization unit.","errorCode":1100}`,
			hint:  hintInvalidOrganizationUnit,
			names: "UIPATH_FOLDER_PATH",
		},
		"1101 no folder reached Orchestrator": {
			body: `{"message":"Organization unit is required.","errorCode":1101}`,
			hint: hintRequiredOrganizationUnit,
		},
		"1850 no reference reached Orchestrator": {
			body: `{"message":"Error creating Transaction. Reference is required for Unique Reference Queues.","errorCode":1850}`,
			hint: hintTransactionReferenceRequired,
		},
	}
	statuses := []int{http.StatusBadRequest, http.StatusForbidden, http.StatusInternalServerError}
	for name, c := range cases {
		for _, status := range statuses {
			t.Run(fmt.Sprintf("%s/%d", name, status), func(t *testing.T) {
				f := newFake(t)
				f.onAPI(func(w http.ResponseWriter, _ capturedRequest) {
					writeJSON(w, status, c.body)
				})
				_, err := f.client().AddQueueItem(context.Background(), "create-abc", json.RawMessage(sampleContent))
				if !errors.Is(err, ErrConfiguration) {
					t.Fatalf("error = %v, want ErrConfiguration for HTTP %d", err, status)
				}
				if errors.Is(err, ErrDuplicateReference) {
					t.Fatalf("error = %v, want it not to be a duplicate", err)
				}
				var apiErr *APIError
				if !errors.As(err, &apiErr) {
					t.Fatalf("error is %T, want *APIError", err)
				}
				if apiErr.Hint != c.hint {
					t.Errorf("Hint = %q, want %q", apiErr.Hint, c.hint)
				}
				if c.names != "" && !strings.Contains(apiErr.Hint, c.names) {
					t.Errorf("Hint = %q, want it to name %s", apiErr.Hint, c.names)
				}
			})
		}
	}
}

// A hint is this client's own sentence, so it must never quote the answer back: the
// answer can carry the patient's name, CNP and phone number, which is why Body exists
// and why only -debug prints it.
func TestAHintNeverCarriesTheAnswer(t *testing.T) {
	const patient = "POPESCU ION 1850412170013 +40700000001"
	f := newFake(t)
	f.onAPI(func(w http.ResponseWriter, _ capturedRequest) {
		writeJSON(w, http.StatusBadRequest,
			fmt.Sprintf(`{"message":"Queue not found for %s","errorCode":1002}`, patient))
	})

	_, err := f.client().AddQueueItem(context.Background(), "create-abc", json.RawMessage(sampleContent))
	var apiErr *APIError
	if !errors.As(err, &apiErr) {
		t.Fatalf("error is %T (%v), want *APIError", err, err)
	}
	if apiErr.Hint == "" {
		t.Fatal("Hint is empty, want the 1002 hint")
	}
	if strings.Contains(apiErr.Hint, patient) {
		t.Errorf("Hint carries the answer's text: %q", apiErr.Hint)
	}
	if !strings.Contains(apiErr.Body, patient) {
		t.Errorf("Body = %q, want it to keep the answer for -debug", apiErr.Body)
	}
}

// INFERRED, not documented: no official page maps a status to this endpoint's permission
// failure, and no code is documented for a caller lacking Queues.View or
// Transactions.Create. A 403 naming no code we know is read as a refused folder, so the
// operator is pointed at the folder and its permissions instead of at exit 1, which
// invites a retry that cannot work.
func TestABareForbiddenIsAConfigurationError(t *testing.T) {
	bodies := map[string]string{
		"no body at all":       ``,
		"not JSON":             `Forbidden`,
		"JSON with no code":    `{"message":"Access to the path is denied."}`,
		"an unrecognised code": `{"message":"Access to the path is denied.","errorCode":9999}`,
	}
	for name, body := range bodies {
		t.Run(name, func(t *testing.T) {
			f := newFake(t)
			f.onAPI(func(w http.ResponseWriter, _ capturedRequest) {
				writeJSON(w, http.StatusForbidden, body)
			})
			_, err := f.client().AddQueueItem(context.Background(), "create-abc", json.RawMessage(sampleContent))
			if !errors.Is(err, ErrConfiguration) {
				t.Fatalf("error = %v, want ErrConfiguration", err)
			}
			var apiErr *APIError
			if !errors.As(err, &apiErr) {
				t.Fatalf("error is %T, want *APIError", err)
			}
			if apiErr.Hint != hintForbidden {
				t.Errorf("Hint = %q, want %q", apiErr.Hint, hintForbidden)
			}
			for _, want := range []string{"UIPATH_FOLDER_PATH", "Queues.View", "Transactions.Create"} {
				if !strings.Contains(apiErr.Hint, want) {
					t.Errorf("Hint = %q, want it to name %s", apiErr.Hint, want)
				}
			}
		})
	}

	t.Run("a read is refused the same way", func(t *testing.T) {
		f := newFake(t)
		f.onAPI(func(w http.ResponseWriter, _ capturedRequest) {
			writeJSON(w, http.StatusForbidden, `{"message":"Access to the path is denied."}`)
		})
		_, err := f.client().LatestQueueItemByReference(context.Background(), "create-abc")
		if !errors.Is(err, ErrConfiguration) {
			t.Fatalf("error = %v, want ErrConfiguration", err)
		}
	})
}

// An error code this client does not know is another failure: exit 1, no hint, and no
// guess at which setting might be at fault.
func TestAnUnknownErrorCodeIsNotAConfigurationError(t *testing.T) {
	bodies := map[string]string{
		"an unknown code": `{"message":"something else went wrong","errorCode":9999}`,
		"a documented code we deliberately do not map": `{"message":"cannot download the package","errorCode":1017}`,
		"no code at all": `{"message":"something else went wrong"}`,
		"not JSON":       `<html><body>Server Error</body></html>`,
	}
	statuses := []int{http.StatusBadRequest, http.StatusInternalServerError, http.StatusServiceUnavailable}
	for name, body := range bodies {
		for _, status := range statuses {
			t.Run(fmt.Sprintf("%s/%d", name, status), func(t *testing.T) {
				f := newFake(t)
				f.onAPI(func(w http.ResponseWriter, _ capturedRequest) {
					writeJSON(w, status, body)
				})
				_, err := f.client().AddQueueItem(context.Background(), "create-abc", json.RawMessage(sampleContent))
				if err == nil {
					t.Fatalf("AddQueueItem: want an error for HTTP %d, got none", status)
				}
				if errors.Is(err, ErrConfiguration) {
					t.Fatalf("error = %v, want something other than ErrConfiguration", err)
				}
				var apiErr *APIError
				if !errors.As(err, &apiErr) {
					t.Fatalf("error is %T, want *APIError", err)
				}
				if apiErr.Hint != "" {
					t.Errorf("Hint = %q, want none: nothing is known about this failure", apiErr.Hint)
				}
			})
		}
	}
}
