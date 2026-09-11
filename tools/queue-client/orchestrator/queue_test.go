package orchestrator

import (
	"context"
	"encoding/json"
	"errors"
	"net/http"
	"os"
	"path/filepath"
	"strings"
	"testing"
)

// validExamplesDir holds the contract v1 example files, from this package's directory.
const validExamplesDir = "../../../contracts/examples/valid"

func TestPrepareAppointmentItemAcceptsEveryValidExample(t *testing.T) {
	entries, err := os.ReadDir(validExamplesDir)
	if err != nil {
		t.Fatalf("reading %s: %v", validExamplesDir, err)
	}
	found := 0
	for _, entry := range entries {
		if entry.IsDir() || !strings.HasSuffix(entry.Name(), ".json") {
			continue
		}
		found++
		name := entry.Name()
		t.Run(name, func(t *testing.T) {
			data, err := os.ReadFile(filepath.Join(validExamplesDir, name))
			if err != nil {
				t.Fatalf("reading the example: %v", err)
			}
			reference, content, err := PrepareAppointmentItem(data)
			if err != nil {
				t.Fatalf("PrepareAppointmentItem: %v", err)
			}
			var fields struct {
				AppointmentId string `json:"AppointmentId"`
			}
			if err := json.Unmarshal(data, &fields); err != nil {
				t.Fatalf("the example is not a JSON object: %v", err)
			}
			if want := ReferencePrefix + fields.AppointmentId; reference != want {
				t.Errorf("reference = %q, want %q", reference, want)
			}
			if strings.ContainsAny(string(content), "\n\t") {
				t.Error("SpecificContent was not compacted")
			}
		})
	}
	if found == 0 {
		t.Fatalf("no .json example in %s", validExamplesDir)
	}
}

func TestPrepareAppointmentItemRejects(t *testing.T) {
	cases := map[string]string{
		"the itemData envelope": `{"itemData":{"Name":"q","SpecificContent":{"AppointmentId":"a"}}}`,
		"no AppointmentId":      `{"SchemaVersion":"1"}`,
		"a non-string id":       `{"AppointmentId":123}`,
		"an empty id":           `{"AppointmentId":"   "}`,
		"a JSON array":          `["AppointmentId"]`,
		"JSON null":             `null`,
		"not JSON at all":       `AppointmentId`,
	}
	for name, input := range cases {
		t.Run(name, func(t *testing.T) {
			if _, _, err := PrepareAppointmentItem([]byte(input)); err == nil {
				t.Fatal("PrepareAppointmentItem: want an error, got none")
			}
		})
	}
}

func TestReferenceFor(t *testing.T) {
	id := "232b0d5d-4321-465f-8533-e25bb5fba40b"
	reference, err := ReferenceFor(id)
	if err != nil {
		t.Fatalf("ReferenceFor: %v", err)
	}
	if want := "create-" + id; reference != want {
		t.Errorf("reference = %q, want %q", reference, want)
	}

	tooLong := strings.Repeat("a", MaxReferenceLength-len(ReferencePrefix)+1)
	if _, err := ReferenceFor(tooLong); err == nil {
		t.Errorf("ReferenceFor: want an error above %d characters, got none", MaxReferenceLength)
	}
	if _, err := ReferenceFor("o'brien"); err == nil {
		t.Error("ReferenceFor: want an error for an apostrophe, got none")
	}
	if _, err := ReferenceFor(""); err == nil {
		t.Error("ReferenceFor: want an error for an empty id, got none")
	}
}

func TestODataStringLiteral(t *testing.T) {
	cases := map[string]string{
		"create-abc": "'create-abc'",
		"o'brien":    "'o''brien'",
		"''":         "''''''",
		"":           "''",
	}
	for input, want := range cases {
		if got := ODataStringLiteral(input); got != want {
			t.Errorf("ODataStringLiteral(%q) = %q, want %q", input, got, want)
		}
	}
}

func TestAddQueueItemBuildsTheItemDataEnvelope(t *testing.T) {
	f := newFake(t)
	f.onAPI(okAddQueueItem)

	reference, content, err := PrepareAppointmentItem([]byte("{\n  \"AppointmentId\": \"abc\",\n  \"ProductName\": \"RMN\"\n}\n"))
	if err != nil {
		t.Fatalf("PrepareAppointmentItem: %v", err)
	}
	item, err := f.client().AddQueueItem(context.Background(), reference, content)
	if err != nil {
		t.Fatalf("AddQueueItem: %v", err)
	}
	if item.ID != 42 {
		t.Errorf("item.ID = %d, want 42", item.ID)
	}

	req := f.apiRequest()
	if req.Method != http.MethodPost {
		t.Errorf("method = %s, want POST", req.Method)
	}
	if req.Path != addQueueItemPath {
		t.Errorf("path = %s, want %s", req.Path, addQueueItemPath)
	}
	wantHeaders := map[string]string{
		"Authorization":       "Bearer " + testToken,
		"Content-Type":        "application/json",
		"X-UIPATH-FolderPath": "PixelData",
	}
	for header, want := range wantHeaders {
		if got := req.Header.Get(header); got != want {
			t.Errorf("header %s = %q, want %q", header, got, want)
		}
	}

	var top map[string]json.RawMessage
	if err := json.Unmarshal([]byte(req.Body), &top); err != nil {
		t.Fatalf("the request body is not a JSON object: %v", err)
	}
	if len(top) != 1 {
		t.Errorf("the request body has %d top-level keys, want only itemData", len(top))
	}
	var envelope struct {
		ItemData struct {
			Name            string          `json:"Name"`
			Priority        string          `json:"Priority"`
			Reference       string          `json:"Reference"`
			SpecificContent json.RawMessage `json:"SpecificContent"`
		} `json:"itemData"`
	}
	if err := json.Unmarshal([]byte(req.Body), &envelope); err != nil {
		t.Fatalf("the request body is not the itemData envelope: %v", err)
	}
	if envelope.ItemData.Name != "PixelData_Programari" {
		t.Errorf("Name = %q, want PixelData_Programari", envelope.ItemData.Name)
	}
	if envelope.ItemData.Priority != Priority {
		t.Errorf("Priority = %q, want %q", envelope.ItemData.Priority, Priority)
	}
	if envelope.ItemData.Reference != "create-abc" {
		t.Errorf("Reference = %q, want create-abc", envelope.ItemData.Reference)
	}
	if want := `{"AppointmentId":"abc","ProductName":"RMN"}`; string(envelope.ItemData.SpecificContent) != want {
		t.Errorf("SpecificContent = %s, want %s", envelope.ItemData.SpecificContent, want)
	}
}

func TestAddQueueItemReportsADuplicateReference(t *testing.T) {
	// The status and the body Orchestrator answers with are (de verificat); the client
	// classifies any non-2xx answer whose body says "duplicate reference".
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

func TestAddQueueItemRefusesBadInput(t *testing.T) {
	f := newFake(t)
	f.onAPI(okAddQueueItem)
	client := f.client()

	if _, err := client.AddQueueItem(context.Background(), "create-o'brien", json.RawMessage(sampleContent)); err == nil {
		t.Error("AddQueueItem: want an error for an apostrophe in the reference, got none")
	}
	if _, err := client.AddQueueItem(context.Background(), "create-abc", json.RawMessage(`[1,2]`)); err == nil {
		t.Error("AddQueueItem: want an error for a SpecificContent that is not an object, got none")
	}
	if requests, _ := f.snapshot(); len(requests) != 0 {
		t.Errorf("%d requests were sent, want 0: bad input must not reach Orchestrator", len(requests))
	}
}

func TestLatestQueueItemByReferenceBuildsTheODataQuery(t *testing.T) {
	f := newFake(t)
	f.onAPI(func(w http.ResponseWriter, _ capturedRequest) {
		writeJSON(w, http.StatusOK, `{"value":[{"Id":7,"Reference":"create-abc","Status":"New"}]}`)
	})

	if _, err := f.client().LatestQueueItemByReference(context.Background(), "create-abc"); err != nil {
		t.Fatalf("LatestQueueItemByReference: %v", err)
	}

	req := f.apiRequest()
	if req.Method != http.MethodGet {
		t.Errorf("method = %s, want GET", req.Method)
	}
	if req.Path != queueItemsPath {
		t.Errorf("path = %s, want %s", req.Path, queueItemsPath)
	}
	if want := "Reference eq 'create-abc'"; req.Query.Get("$filter") != want {
		t.Errorf("$filter = %q, want %q", req.Query.Get("$filter"), want)
	}
	if want := "Id desc"; req.Query.Get("$orderby") != want {
		t.Errorf("$orderby = %q, want %q", req.Query.Get("$orderby"), want)
	}
	if want := "1"; req.Query.Get("$top") != want {
		t.Errorf("$top = %q, want %q", req.Query.Get("$top"), want)
	}
	// The quotes and the spaces travel percent-encoded, never raw and never as '+'.
	if strings.ContainsAny(req.RawQuery, "'+ ") {
		t.Errorf("raw query is not percent-encoded: %s", req.RawQuery)
	}
	if !strings.Contains(req.RawQuery, "%27create-abc%27") {
		t.Errorf("the quoted literal is missing from the raw query: %s", req.RawQuery)
	}
}

// An apostrophe cannot reach the wire any more (ValidateReference refuses it), so the
// doubling in ODataStringLiteral is proved by its own unit test above. This one proves
// the reference is graded here too, on the read path as well as the write path.
func TestLatestQueueItemByReferenceValidatesTheReference(t *testing.T) {
	f := newFake(t)
	f.onAPI(func(w http.ResponseWriter, _ capturedRequest) {
		writeJSON(w, http.StatusOK, `{"value":[]}`)
	})
	client := f.client()

	refused := map[string]string{
		"empty":              "",
		"with an apostrophe": "create-o'brien",
		"too long":           ReferencePrefix + strings.Repeat("a", MaxReferenceLength),
	}
	for name, reference := range refused {
		t.Run(name, func(t *testing.T) {
			if _, err := client.LatestQueueItemByReference(context.Background(), reference); err == nil {
				t.Fatal("LatestQueueItemByReference: want an error, got none")
			}
		})
	}
	if requests, _ := f.snapshot(); len(requests) != 0 {
		t.Errorf("%d requests were sent, want 0: a reference Orchestrator cannot carry never reaches it", len(requests))
	}
}

func TestLatestQueueItemByReferencePicksTheHighestId(t *testing.T) {
	f := newFake(t)
	f.onAPI(func(w http.ResponseWriter, _ capturedRequest) {
		writeJSON(w, http.StatusOK, `{"value":[
			{"Id":10,"Reference":"create-abc","Status":"Retried"},
			{"Id":42,"Reference":"create-abc","Status":"Failed",
			 "ProcessingException":{"Type":"BusinessException","Reason":"CNP_INVALID: cifra de control","Details":"d"}},
			{"Id":7,"Reference":"create-abc","Status":"Retried"}
		]}`)
	})

	item, err := f.client().LatestQueueItemByReference(context.Background(), "create-abc")
	if err != nil {
		t.Fatalf("LatestQueueItemByReference: %v", err)
	}
	if item.ID != 42 {
		t.Fatalf("item.ID = %d, want 42", item.ID)
	}
	if item.Status != "Failed" {
		t.Errorf("item.Status = %q, want Failed", item.Status)
	}
	if item.ProcessingException == nil {
		t.Fatal("item.ProcessingException is nil, want the business exception")
	}
	if !strings.HasPrefix(item.ProcessingException.Reason, "CNP_INVALID:") {
		t.Errorf("Reason = %q, want it to start with the error code", item.ProcessingException.Reason)
	}
}

func TestLatestQueueItemByReferenceNotFound(t *testing.T) {
	f := newFake(t)
	f.onAPI(func(w http.ResponseWriter, _ capturedRequest) {
		writeJSON(w, http.StatusOK, `{"value":[]}`)
	})

	_, err := f.client().LatestQueueItemByReference(context.Background(), "create-missing")
	if !errors.Is(err, ErrNotFound) {
		t.Fatalf("error = %v, want ErrNotFound", err)
	}
}

func TestPrepareAppointmentItemRefusesADuplicateKey(t *testing.T) {
	refused := map[string]string{
		"AppointmentId twice":      `{"AppointmentId":"a","ProductName":"x","AppointmentId":"b"}`,
		"another key twice":        `{"AppointmentId":"a","Notes":"x","Notes":"y"}`,
		"twice in a nested object": `{"AppointmentId":"a","Extra":{"k":1,"k":2}}`,
	}
	for name, input := range refused {
		t.Run(name, func(t *testing.T) {
			_, _, err := PrepareAppointmentItem([]byte(input))
			if err == nil {
				t.Fatal("PrepareAppointmentItem: want an error, got none")
			}
			if !strings.Contains(err.Error(), "twice") {
				t.Errorf("error = %q, want it to say the key appears twice", err)
			}
		})
	}

	accepted := map[string]string{
		"the same key in two different objects": `{"AppointmentId":"a","One":{"k":1},"Two":{"k":2}}`,
		"repeated keys across array elements":   `{"AppointmentId":"a","List":[{"k":1},{"k":2}],"Done":true}`,
		"a key that only looks repeated":        `{"AppointmentId":"a","Notes":"Notes"}`,
	}
	for name, input := range accepted {
		t.Run(name, func(t *testing.T) {
			if _, _, err := PrepareAppointmentItem([]byte(input)); err != nil {
				t.Fatalf("PrepareAppointmentItem: %v", err)
			}
		})
	}
}

// The last value wins in encoding/json, so without the check the reference would be
// built from one value while the item on the wire carried both.
func TestADuplicateKeyNeverReachesOrchestrator(t *testing.T) {
	f := newFake(t)
	f.onAPI(okAddQueueItem)

	_, _, err := PrepareAppointmentItem([]byte(`{"AppointmentId":"first","AppointmentId":"second"}`))
	if err == nil {
		t.Fatal("PrepareAppointmentItem: want an error, got none")
	}
	if requests, _ := f.snapshot(); len(requests) != 0 {
		t.Errorf("%d requests were sent, want 0", len(requests))
	}
}
