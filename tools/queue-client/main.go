// Command queue-client adds one contract v1 appointment to the UiPath Orchestrator
// queue and reads the status of an item back. It is the smoke-test tool of
// docs/06-setup-orchestrator.md section 9 and the reference shape for the phase-3
// dispatcher inside the ScanExpert reception.
//
//	queue-client enqueue -file contracts/examples/valid/cas-cu-bilet.json
//	queue-client status -reference create-<AppointmentId>
//
// Configuration is the seven UIPATH_* environment variables, all required, listed in
// .env.example and in docs/06-setup-orchestrator.md section 11.
package main

import (
	"bytes"
	"context"
	"encoding/json"
	"errors"
	"flag"
	"fmt"
	"io"
	"net/http"
	"os"
	"os/signal"
	"strings"
	"time"

	"pixeldata-programari-rpa/tools/queue-client/orchestrator"
)

// Exit codes. They are part of the documented surface (docs/06 section 9).
const (
	exitOK       = 0 // the command did what was asked
	exitAPI      = 1 // Orchestrator answered with an error, or the network failed
	exitInput    = 2 // configuration, arguments or the input file are wrong
	exitNotFound = 3 // status: no queue item carries that reference
)

const (
	// httpTimeout bounds a single HTTP request.
	httpTimeout = 30 * time.Second
	// commandTimeout bounds a whole command, the token request included.
	commandTimeout = 2 * time.Minute
	// maxInputBytes caps the file read from disk. Orchestrator accepts a
	// SpecificContent of about 256.000 characters (docs/06 section 11).
	maxInputBytes = 1 << 20
	// maxConnsPerHost sizes the connection pool. See newHTTPClient.
	maxConnsPerHost = 16
)

func main() {
	os.Exit(run(os.Args[1:], os.Stdout, os.Stderr))
}

func run(args []string, stdout, stderr io.Writer) int {
	if len(args) == 0 {
		usage(stderr)
		return exitInput
	}
	switch args[0] {
	case "enqueue":
		return enqueue(args[1:], stdout, stderr)
	case "status":
		return status(args[1:], stdout, stderr)
	case "help", "-h", "-help", "--help":
		usage(stdout)
		return exitOK
	default:
		fmt.Fprintf(stderr, "unknown command %q\n\n", args[0])
		usage(stderr)
		return exitInput
	}
}

func usage(w io.Writer) {
	fmt.Fprint(w, `queue-client - put an appointment into the UiPath Orchestrator queue and read its status.

Usage:
  queue-client enqueue -file <path>      add the SpecificContent in <path> to the queue
  queue-client status -reference <ref>   print the latest queue item carrying <ref>

Both commands take -debug, which also prints the body of an Orchestrator error. It is
off by default because such a body quotes the queue item back, patient data included.

The file holds the flat SpecificContent object, never the {"itemData": ...} envelope;
the envelope is built here. The reference is create-<AppointmentId>.

Exit codes:
  0  done (an item already queued under the same reference counts as done)
  1  Orchestrator answered with an error, or the network failed
  2  configuration, arguments or the input file are wrong - including a queue or a
     folder Orchestrator itself refused, where the message names the variable to fix
  3  no queue item carries that reference (status only)

Configuration: the seven UIPATH_* environment variables, all required, no defaults.
See .env.example and docs/06-setup-orchestrator.md section 11.
`)
}

func enqueue(args []string, stdout, stderr io.Writer) int {
	fs := flag.NewFlagSet("enqueue", flag.ContinueOnError)
	fs.SetOutput(stderr)
	file := fs.String("file", "", "path to the JSON file holding the flat SpecificContent object")
	debug := fs.Bool("debug", false, "also print the body of an Orchestrator error (it can carry patient data)")
	if code := parse(fs, args, stderr); code != exitOK {
		return code
	}
	if *file == "" {
		fmt.Fprintln(stderr, "enqueue: -file is required")
		return exitInput
	}
	data, err := readInput(*file)
	if err != nil {
		fmt.Fprintf(stderr, "enqueue: %v\n", err)
		return exitInput
	}
	reference, content, err := orchestrator.PrepareAppointmentItem(data)
	if err != nil {
		fmt.Fprintf(stderr, "enqueue: %s: %v\n", *file, err)
		return exitInput
	}

	client, code := newClient(stderr)
	if client == nil {
		return code
	}
	ctx, cancel := commandContext()
	defer cancel()

	item, err := client.AddQueueItem(ctx, reference, content)
	if err != nil {
		if errors.Is(err, orchestrator.ErrDuplicateReference) {
			// The queue enforces unique references, so this reference is already in
			// it and no second item is wanted: the caller got what it asked for.
			fmt.Fprintf(stdout, "already queued: %s\n", orchestrator.CleanText(reference))
			return exitOK
		}
		reportError(stderr, "enqueue", err, *debug)
		return apiExit(err)
	}
	fmt.Fprintf(stdout, "Id: %d\n", item.ID)
	fmt.Fprintf(stdout, "Reference: %s\n", orchestrator.CleanText(reference))
	if item.Status != "" {
		fmt.Fprintf(stdout, "Status: %s\n", orchestrator.CleanText(item.Status))
	}
	return exitOK
}

func status(args []string, stdout, stderr io.Writer) int {
	fs := flag.NewFlagSet("status", flag.ContinueOnError)
	fs.SetOutput(stderr)
	reference := fs.String("reference", "", "queue item reference, create-<AppointmentId>")
	debug := fs.Bool("debug", false, "also print the body of an Orchestrator error (it can carry patient data)")
	if code := parse(fs, args, stderr); code != exitOK {
		return code
	}
	if *reference == "" {
		fmt.Fprintln(stderr, "status: -reference is required")
		return exitInput
	}
	// A reference Orchestrator cannot carry is the operator's mistake, so it is exit 2
	// here rather than a request that can only come back empty.
	if err := orchestrator.ValidateReference(*reference); err != nil {
		fmt.Fprintf(stderr, "status: %v\n", err)
		return exitInput
	}

	client, code := newClient(stderr)
	if client == nil {
		return code
	}
	ctx, cancel := commandContext()
	defer cancel()

	item, err := client.LatestQueueItemByReference(ctx, *reference)
	if err != nil {
		if errors.Is(err, orchestrator.ErrNotFound) {
			fmt.Fprintf(stderr, "status: no queue item carries the reference %s\n", orchestrator.CleanText(*reference))
			return exitNotFound
		}
		reportError(stderr, "status", err, *debug)
		return apiExit(err)
	}
	printItem(stdout, item)
	return exitOK
}

// reportError prints err and, when Orchestrator's own error code named the cause, this
// client's hint about which setting to look at. The server's answer is printed only when
// the operator asked for it: an Orchestrator error quotes the queue item back, patient
// data included. The hint is not part of that — it is a constant from the orchestrator
// package and carries no byte the server sent, so it is safe without -debug.
func reportError(stderr io.Writer, command string, err error, debug bool) {
	fmt.Fprintf(stderr, "%s: %v\n", command, err)
	var apiErr *orchestrator.APIError
	if !errors.As(err, &apiErr) {
		return
	}
	if apiErr.Hint != "" {
		fmt.Fprintf(stderr, "%s: %s\n", command, apiErr.Hint)
	}
	if debug && apiErr.Body != "" {
		fmt.Fprintf(stderr, "%s: response body: %s\n", command, apiErr.Body)
	}
}

// apiExit is the exit code for a failed call. A failure Orchestrator blamed on how this
// client is configured, or on how it built the request, is exit 2 beside every other
// configuration error: retrying it unchanged cannot help, which is what exit 1 invites.
func apiExit(err error) int {
	if errors.Is(err, orchestrator.ErrConfiguration) {
		return exitInput
	}
	return exitAPI
}

func parse(fs *flag.FlagSet, args []string, stderr io.Writer) int {
	if err := fs.Parse(args); err != nil {
		// flag has already written the message and the flag list to stderr.
		return exitInput
	}
	if fs.NArg() > 0 {
		fmt.Fprintf(stderr, "%s: unexpected argument %q\n", fs.Name(), fs.Arg(0))
		return exitInput
	}
	return exitOK
}

func newClient(stderr io.Writer) (*orchestrator.Client, int) {
	cfg, err := orchestrator.LoadConfig(os.LookupEnv)
	if err != nil {
		fmt.Fprintf(stderr, "configuration: %v\n", err)
		return nil, exitInput
	}
	client, err := orchestrator.NewClient(cfg, newHTTPClient())
	if err != nil {
		fmt.Fprintf(stderr, "configuration: %v\n", err)
		return nil, exitInput
	}
	return client, exitOK
}

// newHTTPClient builds the HTTP client with its connection pool stated rather than
// inherited. This CLI makes two requests in a row and needs one connection; the pool is
// sized for the phase-3 dispatcher, which this file is the template for, because the
// default is two idle connections per host and a fan-out past two would rebuild the TLS
// session for every further item. Clone gives an independent transport, so the shared
// default one is not reconfigured underneath whoever else imports it.
func newHTTPClient() *http.Client {
	transport := http.DefaultTransport.(*http.Transport).Clone()
	transport.MaxIdleConnsPerHost = maxConnsPerHost
	transport.MaxConnsPerHost = maxConnsPerHost
	return &http.Client{Timeout: httpTimeout, Transport: transport}
}

// commandContext gives the command its deadline and stops it on Ctrl-C.
func commandContext() (context.Context, context.CancelFunc) {
	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt)
	ctx, cancel := context.WithTimeout(ctx, commandTimeout)
	return ctx, func() {
		cancel()
		stop()
	}
}

func readInput(path string) ([]byte, error) {
	f, err := os.Open(path)
	if err != nil {
		return nil, err
	}
	defer f.Close()
	info, err := f.Stat()
	if err != nil {
		return nil, err
	}
	if info.IsDir() {
		return nil, fmt.Errorf("%s is a directory", path)
	}
	// A named pipe or a character device would read for as long as something keeps
	// writing, and nothing above this bounds it: the item comes from a file on disk.
	if !info.Mode().IsRegular() {
		return nil, fmt.Errorf("%s is not a regular file (%s)", path, info.Mode().Type())
	}
	data, err := io.ReadAll(io.LimitReader(f, maxInputBytes+1))
	if err != nil {
		return nil, err
	}
	if len(data) > maxInputBytes {
		return nil, fmt.Errorf("%s is larger than %d bytes", path, maxInputBytes)
	}
	if len(bytes.TrimSpace(data)) == 0 {
		return nil, fmt.Errorf("%s is empty", path)
	}
	return data, nil
}

// printItem writes the item. Every value came from Orchestrator, so every one of them
// goes through CleanText first: an escape sequence in a patient name or in a robot
// message would otherwise repaint the operator's terminal.
func printItem(w io.Writer, item orchestrator.QueueItem) {
	fmt.Fprintf(w, "Id: %d\n", item.ID)
	fmt.Fprintf(w, "Reference: %s\n", orchestrator.CleanText(item.Reference))
	fmt.Fprintf(w, "Status: %s\n", orchestrator.CleanText(item.Status))
	if e := item.ProcessingException; e != nil {
		fmt.Fprintf(w, "ProcessingException:\n  Type: %s\n  Reason: %s\n  Details: %s\n",
			orchestrator.CleanText(e.Type), orchestrator.CleanText(e.Reason), orchestrator.CleanText(e.Details))
	} else {
		fmt.Fprintln(w, "ProcessingException: none")
	}
	fmt.Fprintf(w, "Output: %s\n", outputText(item))
}

// outputText renders the robot's Output object on one cleaned line. It is compacted
// rather than indented, because indenting would mean printing the server's bytes with
// their own newlines and there would then be no single line left to make safe.
func outputText(item orchestrator.QueueItem) string {
	if raw := strings.TrimSpace(string(item.Output)); raw != "" && raw != "null" {
		var compact bytes.Buffer
		if err := json.Compact(&compact, []byte(raw)); err == nil {
			return orchestrator.CleanText(compact.String())
		}
		return orchestrator.CleanText(raw)
	}
	if item.OutputData != "" {
		return orchestrator.CleanText(item.OutputData)
	}
	return "none"
}
