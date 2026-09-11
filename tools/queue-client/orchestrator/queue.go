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
	"unicode/utf8"
)

const (
	// MaxReferenceLength is Orchestrator's limit on a queue item Reference.
	MaxReferenceLength = 128
	// ReferencePrefix + AppointmentId is the Reference of a contract v1 create item.
	ReferencePrefix = "create-"
	// Priority is the priority of every item the client adds.
	Priority = "Normal"

	opAddQueueItem = "AddQueueItem"
	opQueueItems   = "QueueItems"
)

// QueueItem is the part of an Orchestrator queue item the client reads.
type QueueItem struct {
	ID                  int64                `json:"Id"`
	Key                 string               `json:"Key"`
	Reference           string               `json:"Reference"`
	Status              string               `json:"Status"` // New, InProgress, Successful, Failed, Abandoned, Retried
	ProcessingException *ProcessingException `json:"ProcessingException"`
	// Output is the robot's output object as Orchestrator returns it (raw JSON, may be null).
	Output json.RawMessage `json:"Output"`
	// OutputData is the output serialized as a string, when the answer carries it (de verificat).
	OutputData string `json:"OutputData"`
}

// ProcessingException is kept by Orchestrator when a transaction fails. By contract the
// robot's Reason starts with the error code, e.g. "CNP_INVALID: <message>".
type ProcessingException struct {
	Type    string `json:"Type"` // BusinessException or ApplicationException
	Reason  string `json:"Reason"`
	Details string `json:"Details"`
}

type addQueueItemRequest struct {
	ItemData queueItemData `json:"itemData"`
}

type queueItemData struct {
	Name            string          `json:"Name"`
	Priority        string          `json:"Priority"`
	Reference       string          `json:"Reference"`
	SpecificContent json.RawMessage `json:"SpecificContent"`
}

// PrepareAppointmentItem checks that data is a flat SpecificContent object (not the
// {"itemData": ...} envelope) with a non-empty string AppointmentId, and returns its
// Reference and the compacted object. It does not check the rest of contract v1:
// contracts/validate_examples.py and the robot do.
func PrepareAppointmentItem(data []byte) (string, json.RawMessage, error) {
	var fields map[string]json.RawMessage
	if err := json.Unmarshal(data, &fields); err != nil {
		return "", nil, fmt.Errorf("SpecificContent must be a JSON object: %v", err)
	}
	if fields == nil {
		return "", nil, errors.New("SpecificContent must be a JSON object, not null")
	}
	if _, ok := fields["itemData"]; ok {
		return "", nil, errors.New(`this is the {"itemData": ...} envelope; pass only the flat SpecificContent object`)
	}
	if err := rejectDuplicateKeys(data); err != nil {
		return "", nil, err
	}
	rawID, ok := fields["AppointmentId"]
	if !ok {
		return "", nil, errors.New("SpecificContent has no AppointmentId")
	}
	var id string
	if err := json.Unmarshal(rawID, &id); err != nil {
		return "", nil, errors.New("AppointmentId must be a JSON string")
	}
	reference, err := ReferenceFor(id)
	if err != nil {
		return "", nil, err
	}
	var compact bytes.Buffer
	if err := json.Compact(&compact, data); err != nil {
		return "", nil, fmt.Errorf("SpecificContent is not valid JSON: %v", err)
	}
	return reference, json.RawMessage(compact.Bytes()), nil
}

// jsonFrame is one open object or array while rejectDuplicateKeys walks a document.
type jsonFrame struct {
	object  bool
	wantKey bool
	keys    map[string]struct{}
}

// rejectDuplicateKeys reports the first key that appears twice in the same object.
//
// It matters because the two ends read such a document differently: encoding/json keeps
// the LAST value of a repeated key, so the Reference would be built from one value,
// while the item on the wire still carries both and the robot's parser may take the
// first. The appointment would then be queued under a reference that does not describe
// it. contracts/validate_examples.py refuses the same thing in the example files.
func rejectDuplicateKeys(data []byte) error {
	dec := json.NewDecoder(bytes.NewReader(data))
	var stack []*jsonFrame
	pop := func() {
		stack = stack[:len(stack)-1]
		if len(stack) > 0 && stack[len(stack)-1].object {
			stack[len(stack)-1].wantKey = true
		}
	}
	for {
		token, err := dec.Token()
		if errors.Is(err, io.EOF) {
			return nil
		}
		if err != nil {
			return fmt.Errorf("SpecificContent is not valid JSON: %v", err)
		}
		delim, isDelim := token.(json.Delim)
		top := (*jsonFrame)(nil)
		if len(stack) > 0 {
			top = stack[len(stack)-1]
		}

		// Inside an object, a token is a key unless it closes the object.
		if top != nil && top.object && top.wantKey {
			if isDelim && delim == '}' {
				pop()
				continue
			}
			key, ok := token.(string)
			if !ok {
				return fmt.Errorf("SpecificContent is not valid JSON: %v is not a key", token)
			}
			if _, seen := top.keys[key]; seen {
				return fmt.Errorf("the key %q appears twice in the same object", key)
			}
			top.keys[key] = struct{}{}
			top.wantKey = false
			continue
		}

		switch {
		case isDelim && (delim == '{' || delim == '['):
			stack = append(stack, &jsonFrame{
				object:  delim == '{',
				wantKey: delim == '{',
				keys:    map[string]struct{}{},
			})
		case isDelim:
			pop()
		case top != nil && top.object:
			top.wantKey = true
		}
	}
}

// ReferenceFor builds the queue Reference of an appointment: create-<AppointmentId>.
func ReferenceFor(appointmentID string) (string, error) {
	if strings.TrimSpace(appointmentID) == "" {
		return "", errors.New("AppointmentId is empty")
	}
	reference := ReferencePrefix + appointmentID
	if err := ValidateReference(reference); err != nil {
		return "", err
	}
	return reference, nil
}

// ValidateReference reports why Orchestrator could not carry this Reference: it is
// empty, longer than MaxReferenceLength, or holds an apostrophe, which is the one
// character that cannot appear in the OData filter that reads the item back.
func ValidateReference(reference string) error {
	if reference == "" {
		return errors.New("reference is empty")
	}
	if n := utf8.RuneCountInString(reference); n > MaxReferenceLength {
		return fmt.Errorf("reference is %d characters; Orchestrator allows at most %d", n, MaxReferenceLength)
	}
	if strings.Contains(reference, "'") {
		return errors.New("reference must not contain an apostrophe (')")
	}
	return nil
}

// AddQueueItem adds one item to the configured queue with Priority Normal. When the
// queue already holds the Reference the error matches ErrDuplicateReference.
func (c *Client) AddQueueItem(ctx context.Context, reference string, specificContent json.RawMessage) (QueueItem, error) {
	if err := ValidateReference(reference); err != nil {
		return QueueItem{}, fmt.Errorf("orchestrator: %s: %w", opAddQueueItem, err)
	}
	trimmed := bytes.TrimSpace(specificContent)
	if len(trimmed) == 0 || trimmed[0] != '{' || !json.Valid(trimmed) {
		return QueueItem{}, fmt.Errorf("orchestrator: %s: SpecificContent must be a JSON object", opAddQueueItem)
	}
	payload, err := json.Marshal(addQueueItemRequest{ItemData: queueItemData{
		Name:            c.cfg.QueueName,
		Priority:        Priority,
		Reference:       reference,
		SpecificContent: trimmed,
	}})
	if err != nil {
		return QueueItem{}, fmt.Errorf("orchestrator: %s: %w", opAddQueueItem, err)
	}
	body, err := c.call(ctx, opAddQueueItem, http.MethodPost, "odata/Queues/UiPathODataSvc.AddQueueItem", payload)
	if err != nil {
		return QueueItem{}, err
	}
	var item QueueItem
	if err := json.Unmarshal(body, &item); err != nil {
		return QueueItem{}, fmt.Errorf("orchestrator: %s: response is not a queue item", opAddQueueItem)
	}
	return item, nil
}

// LatestQueueItemByReference returns the item with the highest Id carrying reference.
// Retries of an item keep its Reference, so the highest Id is the latest attempt.
//
// The query asks Orchestrator for that one item ($orderby=Id desc, $top=1), and the
// highest Id is then taken from whatever the answer holds, so the caller gets the
// latest attempt even if the page comes back in another order.
func (c *Client) LatestQueueItemByReference(ctx context.Context, reference string) (QueueItem, error) {
	if err := ValidateReference(reference); err != nil {
		return QueueItem{}, fmt.Errorf("orchestrator: %s: %w", opQueueItems, err)
	}
	query := "$filter=" + queryEscape("Reference eq "+ODataStringLiteral(reference)) +
		"&$orderby=" + queryEscape("Id desc") +
		"&$top=1"
	body, err := c.call(ctx, opQueueItems, http.MethodGet, "odata/QueueItems?"+query, nil)
	if err != nil {
		return QueueItem{}, err
	}
	var page struct {
		Value []QueueItem `json:"value"`
	}
	if err := json.Unmarshal(body, &page); err != nil {
		return QueueItem{}, fmt.Errorf("orchestrator: %s: response is not an OData page", opQueueItems)
	}
	if len(page.Value) == 0 {
		return QueueItem{}, ErrNotFound
	}
	latest := page.Value[0]
	for _, item := range page.Value[1:] {
		if item.ID > latest.ID {
			latest = item
		}
	}
	return latest, nil
}

// ODataStringLiteral quotes s as an OData string literal. Every apostrophe inside s is
// doubled, which is how OData escapes one:
//
//	O'Brien  ->  'O''Brien'
func ODataStringLiteral(s string) string {
	return "'" + strings.ReplaceAll(s, "'", "''") + "'"
}

// queryEscape percent-encodes a query value, spaces as %20 rather than +.
func queryEscape(s string) string {
	return strings.ReplaceAll(url.QueryEscape(s), "+", "%20")
}
