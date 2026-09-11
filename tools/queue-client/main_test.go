package main

import (
	"bytes"
	"encoding/json"
	"fmt"
	"net/http"
	"os"
	"path/filepath"
	"strings"
	"syscall"
	"testing"

	"pixeldata-programari-rpa/tools/queue-client/orchestrator"
)

// esc is the byte that starts a terminal escape sequence.
const esc = "\x1b"

// Finding 4: every value printed here came from Orchestrator, so an escape sequence in
// a patient name or in a robot message would otherwise repaint the operator's terminal.
func TestPrintItemCleansEveryValueFromTheServer(t *testing.T) {
	item := orchestrator.QueueItem{
		ID:        7,
		Reference: "create-" + esc + "[2J",
		Status:    "Failed" + esc + "[31m",
		ProcessingException: &orchestrator.ProcessingException{
			Type:    "BusinessException" + esc + "[0m",
			Reason:  "CNP_INVALID: " + esc + "]0;owned\x07",
			Details: "line one\nline two",
		},
	}

	var out bytes.Buffer
	printItem(&out, item)
	printed := out.String()

	if strings.Contains(printed, esc) {
		t.Errorf("the output carries an escape character: %q", printed)
	}
	if strings.Count(printed, "\n") != 8 {
		t.Errorf("the output has %d lines, want 8: a newline in Details must not add one", strings.Count(printed, "\n"))
	}
	for _, want := range []string{"Id: 7", "CNP_INVALID:", "BusinessException"} {
		if !strings.Contains(printed, want) {
			t.Errorf("the output lost %q: %s", want, printed)
		}
	}
}

func TestPrintItemWithoutAnException(t *testing.T) {
	var out bytes.Buffer
	printItem(&out, orchestrator.QueueItem{ID: 1, Reference: "create-abc", Status: "Successful"})
	if !strings.Contains(out.String(), "ProcessingException: none") {
		t.Errorf("output = %q, want it to say there is no exception", out.String())
	}
}

func TestOutputText(t *testing.T) {
	cases := map[string]struct {
		item orchestrator.QueueItem
		want string
	}{
		"valid JSON is compacted": {
			item: orchestrator.QueueItem{Output: json.RawMessage("{\n  \"Outcome\": \"created\"\n}")},
			want: `{"Outcome":"created"}`,
		},
		"a JSON-escaped control character is left alone": {
			item: orchestrator.QueueItem{Output: json.RawMessage(`{"Outcome":"\u001b[31m"}`)},
			want: `{"Outcome":"\u001b[31m"}`,
		},
		"null means none": {
			item: orchestrator.QueueItem{Output: json.RawMessage("null")},
			want: "none",
		},
		"nothing at all means none": {
			item: orchestrator.QueueItem{},
			want: "none",
		},
		"OutputData is used when Output is empty": {
			item: orchestrator.QueueItem{OutputData: "Outcome=created"},
			want: "Outcome=created",
		},
		"OutputData is cleaned": {
			item: orchestrator.QueueItem{OutputData: "Outcome=" + esc + "[31m"},
			want: "Outcome= [31m",
		},
		"a raw control byte makes it invalid JSON and it is cleaned": {
			item: orchestrator.QueueItem{Output: json.RawMessage("{\"Outcome\":\"" + esc + "[31m\"}")},
			want: `{"Outcome":" [31m"}`,
		},
	}
	for name, c := range cases {
		t.Run(name, func(t *testing.T) {
			if got := outputText(c.item); got != c.want {
				t.Errorf("outputText = %q, want %q", got, c.want)
			}
		})
	}
}

// Finding 1: the body of an Orchestrator error is printed only when asked for.
func TestReportErrorPrintsTheBodyOnlyWithDebug(t *testing.T) {
	const patient = "POPESCU ION 1850412170013"
	err := &orchestrator.APIError{
		Op:         "AddQueueItem",
		StatusCode: http.StatusBadRequest,
		Body:       "invalid item for " + patient,
	}

	var quiet bytes.Buffer
	reportError(&quiet, "enqueue", err, false)
	if strings.Contains(quiet.String(), patient) {
		t.Errorf("the default output carries the answer's text: %q", quiet.String())
	}
	if !strings.Contains(quiet.String(), "400") {
		t.Errorf("output = %q, want the status", quiet.String())
	}

	var loud bytes.Buffer
	reportError(&loud, "enqueue", err, true)
	if !strings.Contains(loud.String(), patient) {
		t.Errorf("-debug output = %q, want the answer's text", loud.String())
	}

	t.Run("a plain error carries no body line", func(t *testing.T) {
		var out bytes.Buffer
		reportError(&out, "status", fmt.Errorf("the network is down"), true)
		if strings.Contains(out.String(), "response body") {
			t.Errorf("output = %q, want no body line", out.String())
		}
	})
}

// The hint names the setting to fix, so withholding it until -debug would hide the one
// line that ends the problem. It is safe to print because the orchestrator package writes
// it, unlike Body, which is the server's own bytes.
func TestReportErrorPrintsTheHintWithoutDebug(t *testing.T) {
	const patient = "POPESCU ION 1850412170013"
	err := &orchestrator.APIError{
		Op:         "AddQueueItem",
		StatusCode: http.StatusForbidden,
		Body:       "denied for " + patient,
		Hint:       "check UIPATH_FOLDER_PATH and the Queues.View permission there.",
	}

	var quiet bytes.Buffer
	reportError(&quiet, "enqueue", err, false)
	if !strings.Contains(quiet.String(), "UIPATH_FOLDER_PATH") {
		t.Errorf("output = %q, want the hint without -debug", quiet.String())
	}
	if strings.Contains(quiet.String(), patient) {
		t.Errorf("the default output carries the answer's text: %q", quiet.String())
	}

	t.Run("no hint, no extra line", func(t *testing.T) {
		var out bytes.Buffer
		reportError(&out, "enqueue", &orchestrator.APIError{Op: "AddQueueItem", StatusCode: 500}, false)
		if strings.Count(out.String(), "\n") != 1 {
			t.Errorf("output = %q, want one line when there is no hint", out.String())
		}
	})
}

// A failure Orchestrator blamed on the configuration is the operator's to fix, so it
// joins every other configuration error at exit 2 rather than sitting at exit 1, where
// it reads as something a retry might clear.
func TestApiExitSeparatesConfigurationFromEverythingElse(t *testing.T) {
	cases := map[string]struct {
		err  error
		want int
	}{
		"a configuration failure": {
			err:  fmt.Errorf("enqueue: %w", orchestrator.ErrConfiguration),
			want: exitInput,
		},
		"another API failure": {
			err:  &orchestrator.APIError{Op: "AddQueueItem", StatusCode: http.StatusInternalServerError},
			want: exitAPI,
		},
		"the network failed": {
			err:  fmt.Errorf("the network is down"),
			want: exitAPI,
		},
	}
	for name, c := range cases {
		t.Run(name, func(t *testing.T) {
			if got := apiExit(c.err); got != c.want {
				t.Errorf("apiExit = %d, want %d", got, c.want)
			}
		})
	}
}

func TestReadInput(t *testing.T) {
	dir := t.TempDir()
	write := func(name, content string) string {
		path := filepath.Join(dir, name)
		if err := os.WriteFile(path, []byte(content), 0o600); err != nil {
			t.Fatalf("writing the fixture: %v", err)
		}
		return path
	}

	t.Run("an ordinary file", func(t *testing.T) {
		data, err := readInput(write("ok.json", `{"AppointmentId":"a"}`))
		if err != nil {
			t.Fatalf("readInput: %v", err)
		}
		if string(data) != `{"AppointmentId":"a"}` {
			t.Errorf("data = %q", data)
		}
	})

	t.Run("a missing file", func(t *testing.T) {
		if _, err := readInput(filepath.Join(dir, "absent.json")); err == nil {
			t.Fatal("readInput: want an error, got none")
		}
	})

	t.Run("a directory", func(t *testing.T) {
		if _, err := readInput(dir); err == nil {
			t.Fatal("readInput: want an error for a directory, got none")
		}
	})

	t.Run("an empty file", func(t *testing.T) {
		if _, err := readInput(write("empty.json", "   \n")); err == nil {
			t.Fatal("readInput: want an error for an empty file, got none")
		}
	})

	t.Run("a file over the cap", func(t *testing.T) {
		_, err := readInput(write("big.json", strings.Repeat("a", maxInputBytes+1)))
		if err == nil {
			t.Fatal("readInput: want an error above the cap, got none")
		}
		if !strings.Contains(err.Error(), "larger than") {
			t.Errorf("error = %q, want it to say the file is too large", err)
		}
	})

	t.Run("a file exactly at the cap", func(t *testing.T) {
		if _, err := readInput(write("edge.json", strings.Repeat("a", maxInputBytes))); err != nil {
			t.Fatalf("readInput at exactly the cap: %v", err)
		}
	})

	t.Run("a named pipe", func(t *testing.T) {
		pipe := filepath.Join(dir, "fifo")
		if err := syscall.Mkfifo(pipe, 0o600); err != nil {
			t.Skipf("this system has no named pipes: %v", err)
		}
		// Opened read-write so the open itself does not block on a missing writer:
		// the test grades the refusal, not the deadlock the refusal exists to avoid.
		keepOpen, err := os.OpenFile(pipe, os.O_RDWR, 0)
		if err != nil {
			t.Fatalf("opening the pipe: %v", err)
		}
		defer keepOpen.Close()

		if _, err := readInput(pipe); err == nil {
			t.Fatal("readInput: want an error for a named pipe, got none")
		}
	})
}

func TestRunRejectsBadArguments(t *testing.T) {
	cases := [][]string{
		{},
		{"unknown"},
		{"enqueue"},
		{"status"},
		{"enqueue", "-file", "x.json", "extra"},
		{"status", "-reference", "create-o'brien"},
		{"status", "-reference", "create-" + strings.Repeat("a", 200)},
	}
	for _, args := range cases {
		t.Run(strings.Join(args, " "), func(t *testing.T) {
			var stdout, stderr bytes.Buffer
			if code := run(args, &stdout, &stderr); code != exitInput {
				t.Errorf("run(%q) = %d, want %d", args, code, exitInput)
			}
			if stderr.Len() == 0 {
				t.Error("nothing was written to stderr")
			}
		})
	}
}

func TestRunHelpSucceeds(t *testing.T) {
	var stdout, stderr bytes.Buffer
	if code := run([]string{"help"}, &stdout, &stderr); code != exitOK {
		t.Errorf("run(help) = %d, want %d", code, exitOK)
	}
	if !strings.Contains(stdout.String(), "-debug") {
		t.Error("the usage text does not mention -debug")
	}
}

// The default transport keeps 2 idle connections per host, which the phase-3 dispatcher
// would exceed the moment it worked on more than two items at once.
func TestNewHTTPClientStatesItsConnectionPool(t *testing.T) {
	client := newHTTPClient()
	if client.Timeout != httpTimeout {
		t.Errorf("Timeout = %s, want %s", client.Timeout, httpTimeout)
	}
	transport, ok := client.Transport.(*http.Transport)
	if !ok {
		t.Fatalf("Transport is %T, want *http.Transport", client.Transport)
	}
	if transport.MaxIdleConnsPerHost != maxConnsPerHost {
		t.Errorf("MaxIdleConnsPerHost = %d, want %d", transport.MaxIdleConnsPerHost, maxConnsPerHost)
	}
	if transport.MaxConnsPerHost != maxConnsPerHost {
		t.Errorf("MaxConnsPerHost = %d, want %d", transport.MaxConnsPerHost, maxConnsPerHost)
	}
	if transport == http.DefaultTransport {
		t.Error("the shared default transport was reconfigured instead of a clone")
	}
	if transport.TLSClientConfig != nil && transport.TLSClientConfig.InsecureSkipVerify {
		t.Error("the clone turned off certificate verification")
	}
}
