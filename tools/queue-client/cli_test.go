package main

// End-to-end tests through run(), against a fake Orchestrator. They grade the exit codes
// docs/06-setup-orchestrator.md section 9 promises an operator, and the message that comes
// with each one: an exit code is only useful if the line beside it says what to fix.
//
// Everything below goes through the same run() main calls. The only things replaced are
// the three in cli: the environment, the output streams, and the HTTP client, the last
// only because the fake server speaks TLS with its own certificate.

import (
	"bytes"
	"fmt"
	"io"
	"net/http"
	"net/http/httptest"
	"os"
	"path/filepath"
	"strings"
	"testing"
	"time"

	"pixeldata-programari-rpa/tools/queue-client/orchestrator"
)

const (
	// The values the fake environment carries. The secret is asserted never to appear
	// in any output, exactly as the orchestrator package's own tests assert it.
	cliTestSecret = "SECRET-do-not-print-e41c"
	cliTestToken  = "TOKEN-do-not-print-b92f"
	cliTestOrg    = "acme"
)

// fakeOrchestrator serves the token endpoint and hands every other request to answer.
func fakeOrchestrator(t *testing.T, answer func(w http.ResponseWriter)) *httptest.Server {
	t.Helper()
	server := httptest.NewTLSServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if strings.HasSuffix(r.URL.Path, "/connect/token") {
			w.Header().Set("Content-Type", "application/json")
			fmt.Fprintf(w, `{"access_token":%q,"expires_in":3600,"token_type":"Bearer"}`, cliTestToken)
			return
		}
		answer(w)
	}))
	t.Cleanup(server.Close)
	t.Cleanup(server.Client().CloseIdleConnections)
	return server
}

// testCLI wires run() to the fake server and a complete environment. Drop a variable from
// env to test a missing one.
func testCLI(t *testing.T, server *httptest.Server, drop string) (cli, *bytes.Buffer, *bytes.Buffer) {
	t.Helper()
	env := map[string]string{
		orchestrator.EnvCloudURL:     server.URL,
		orchestrator.EnvOrg:          cliTestOrg,
		orchestrator.EnvTenant:       "DefaultTenant",
		orchestrator.EnvClientID:     "client-id",
		orchestrator.EnvClientSecret: cliTestSecret,
		orchestrator.EnvFolderPath:   "PixelData",
		orchestrator.EnvQueueName:    "PixelData_Programari",
	}
	delete(env, drop)
	httpClient := server.Client()
	httpClient.Timeout = 10 * time.Second

	var stdout, stderr bytes.Buffer
	return cli{
		lookupEnv:  func(name string) (string, bool) { value, ok := env[name]; return value, ok },
		stdout:     &stdout,
		stderr:     &stderr,
		httpClient: httpClient,
	}, &stdout, &stderr
}

// writeItem puts a valid flat SpecificContent object on disk and returns its path.
func writeItem(t *testing.T) string {
	t.Helper()
	path := filepath.Join(t.TempDir(), "item.json")
	if err := os.WriteFile(path, []byte(`{"AppointmentId":"232b0d5d-4321-465f-8533-e25bb5fba40b"}`), 0o600); err != nil {
		t.Fatalf("writing the fixture: %v", err)
	}
	return path
}

func answerJSON(status int, body string) func(w http.ResponseWriter) {
	return func(w http.ResponseWriter) {
		w.Header().Set("Content-Type", "application/json")
		w.WriteHeader(status)
		io.WriteString(w, body)
	}
}

// Every exit code docs/06 section 9 promises, taken end to end through run(), with the
// thing each message has to name beside it. The HTTP statuses vary on purpose: none of
// these classifications may rest on one.
func TestRunExitCodes(t *testing.T) {
	cases := map[string]struct {
		answer func(w http.ResponseWriter)
		args   func(itemPath string) []string
		drop   string
		want   int
		// names is what stderr (or stdout, for exit 0) must mention, so the operator is
		// told what to fix rather than only that something failed. absent is what it must
		// NOT mention, which is how a case proves it failed where it claims to.
		names  []string
		absent []string
	}{
		// These three name a variable AND the operation. A configuration failure exits 2
		// and names a variable too, so without the operation they would pass even if the
		// environment never reached Orchestrator and the call was never made.
		"1002 sends the operator to the queue name": {
			answer: answerJSON(http.StatusNotFound, `{"message":"Queue not found.","errorCode":1002}`),
			want:   exitInput,
			names:  []string{"AddQueueItem", "404", orchestrator.EnvQueueName, orchestrator.EnvFolderPath},
		},
		"1100 sends the operator to the folder": {
			answer: answerJSON(http.StatusBadRequest, `{"message":"Invalid organization unit.","errorCode":1100}`),
			want:   exitInput,
			names:  []string{"AddQueueItem", "400", orchestrator.EnvFolderPath},
		},
		"a bare 403 sends the operator to the folder and its permissions": {
			answer: answerJSON(http.StatusForbidden, `{"message":"Access to the path is denied."}`),
			want:   exitInput,
			names:  []string{"AddQueueItem", "403", orchestrator.EnvFolderPath, "Queues.View", "Transactions.Create"},
		},
		"1016 is not a failure at all": {
			answer: answerJSON(http.StatusConflict, `{"message":"Error creating Transaction. Duplicate Reference.","errorCode":1016}`),
			want:   exitOK,
			names:  []string{"already queued", "create-232b0d5d-4321-465f-8533-e25bb5fba40b"},
		},
		"an unknown error code stays an API failure": {
			answer: answerJSON(http.StatusInternalServerError, `{"message":"something else went wrong","errorCode":9999}`),
			want:   exitAPI,
			names:  []string{"AddQueueItem", "500"},
		},
		// The mirror of those three: this one must fail BEFORE any call, so it names the
		// configuration step and the variable, and never the operation.
		"a missing environment variable is named": {
			answer: answerJSON(http.StatusCreated, `{"Id":1}`),
			drop:   orchestrator.EnvQueueName,
			want:   exitInput,
			names:  []string{"configuration", orchestrator.EnvQueueName},
			absent: []string{"AddQueueItem"},
		},
		"a queued item prints its id": {
			answer: answerJSON(http.StatusCreated, `{"Id":42,"Reference":"create-232b0d5d-4321-465f-8533-e25bb5fba40b","Status":"New"}`),
			want:   exitOK,
			names:  []string{"Id: 42", "Status: New"},
		},
		"status finds nothing": {
			answer: answerJSON(http.StatusOK, `{"value":[]}`),
			args: func(string) []string {
				return []string{"status", "-reference", "create-232b0d5d-4321-465f-8533-e25bb5fba40b"}
			},
			want:  exitNotFound,
			names: []string{"create-232b0d5d-4321-465f-8533-e25bb5fba40b"},
		},
		"status reads an item back": {
			answer: answerJSON(http.StatusOK, `{"value":[{"Id":7,"Reference":"create-232b0d5d-4321-465f-8533-e25bb5fba40b","Status":"Successful"}]}`),
			args: func(string) []string {
				return []string{"status", "-reference", "create-232b0d5d-4321-465f-8533-e25bb5fba40b"}
			},
			want:  exitOK,
			names: []string{"Id: 7", "Status: Successful"},
		},
		"status on a refused folder is a configuration failure too": {
			answer: answerJSON(http.StatusForbidden, `{"message":"Access to the path is denied."}`),
			args: func(string) []string {
				return []string{"status", "-reference", "create-232b0d5d-4321-465f-8533-e25bb5fba40b"}
			},
			want:  exitInput,
			names: []string{orchestrator.EnvFolderPath},
		},
	}

	for name, c := range cases {
		t.Run(name, func(t *testing.T) {
			server := fakeOrchestrator(t, c.answer)
			client, stdout, stderr := testCLI(t, server, c.drop)

			itemPath := writeItem(t)
			args := []string{"enqueue", "-file", itemPath}
			if c.args != nil {
				args = c.args(itemPath)
			}

			if code := run(args, client); code != c.want {
				t.Fatalf("run(%q) = %d, want %d\nstdout: %s\nstderr: %s", args, code, c.want, stdout, stderr)
			}
			printed := stdout.String() + stderr.String()
			for _, want := range c.names {
				if !strings.Contains(printed, want) {
					t.Errorf("output does not name %q:\n%s", want, printed)
				}
			}
			for _, unwanted := range c.absent {
				if strings.Contains(printed, unwanted) {
					t.Errorf("output names %q, so it failed somewhere other than where this case claims:\n%s", unwanted, printed)
				}
			}
			if strings.Contains(printed, cliTestSecret) {
				t.Errorf("the output carries the client secret:\n%s", printed)
			}
			if strings.Contains(printed, cliTestToken) {
				t.Errorf("the output carries the access token:\n%s", printed)
			}
		})
	}
}

// The patient-data rule, end to end: an Orchestrator error quotes the queue item back, so
// the default run must not print it, and -debug must. Tested here as well as at the unit
// level because it is the whole reason -debug exists, and the two halves could agree while
// the wiring between them did not.
func TestRunPrintsPatientDataOnlyWithDebug(t *testing.T) {
	const (
		name = "POPESCU ION"
		cnp  = "1850412170013"
	)
	answer := answerJSON(http.StatusBadRequest,
		fmt.Sprintf(`{"message":"invalid item for %s %s","errorCode":9999}`, name, cnp))

	t.Run("without -debug", func(t *testing.T) {
		server := fakeOrchestrator(t, answer)
		client, stdout, stderr := testCLI(t, server, "")
		code := run([]string{"enqueue", "-file", writeItem(t)}, client)

		printed := stdout.String() + stderr.String()
		if code != exitAPI {
			t.Fatalf("run = %d, want %d\n%s", code, exitAPI, printed)
		}
		for _, secret := range []string{name, cnp} {
			if strings.Contains(printed, secret) {
				t.Errorf("the default output carries %q:\n%s", secret, printed)
			}
		}
		if !strings.Contains(printed, "400") {
			t.Errorf("output = %q, want it to name the status", printed)
		}
	})

	t.Run("with -debug", func(t *testing.T) {
		server := fakeOrchestrator(t, answer)
		client, stdout, stderr := testCLI(t, server, "")
		code := run([]string{"enqueue", "-file", writeItem(t), "-debug"}, client)

		printed := stdout.String() + stderr.String()
		if code != exitAPI {
			t.Fatalf("run = %d, want %d\n%s", code, exitAPI, printed)
		}
		for _, wanted := range []string{name, cnp} {
			if !strings.Contains(printed, wanted) {
				t.Errorf("-debug output lost %q:\n%s", wanted, printed)
			}
		}
	})
}

// A hint names a setting, never the appointment, so it is printed without -debug. This is
// the end-to-end half of the same rule: a configuration failure whose answer happens to
// quote the patient still tells the operator which variable to fix and nothing more.
func TestRunPrintsTheHintButNotThePatient(t *testing.T) {
	const cnp = "1850412170013"
	server := fakeOrchestrator(t, answerJSON(http.StatusNotFound,
		fmt.Sprintf(`{"message":"Queue not found while queueing %s","errorCode":1002}`, cnp)))
	client, stdout, stderr := testCLI(t, server, "")

	if code := run([]string{"enqueue", "-file", writeItem(t)}, client); code != exitInput {
		t.Fatalf("run = %d, want %d", code, exitInput)
	}
	printed := stdout.String() + stderr.String()
	if !strings.Contains(printed, orchestrator.EnvQueueName) {
		t.Errorf("output does not name the variable to fix:\n%s", printed)
	}
	if strings.Contains(printed, cnp) {
		t.Errorf("the default output carries the patient's CNP:\n%s", printed)
	}
}
