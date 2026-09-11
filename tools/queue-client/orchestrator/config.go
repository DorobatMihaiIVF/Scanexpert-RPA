// Package orchestrator is a minimal UiPath Orchestrator (Automation Cloud) client that
// adds items to a queue and reads them back, using only the standard library. It is
// the reference shape for the phase-3 dispatcher inside the ScanExpert reception.
package orchestrator

import (
	"fmt"
	"net/url"
	"strings"
)

// Scope is the only OAuth scope the client requests.
const Scope = "OR.Queues"

// Environment variable names. Every one is required; there are no defaults.
const (
	EnvCloudURL     = "UIPATH_CLOUD_URL"
	EnvOrg          = "UIPATH_ORG"
	EnvTenant       = "UIPATH_TENANT"
	EnvClientID     = "UIPATH_CLIENT_ID"
	EnvClientSecret = "UIPATH_CLIENT_SECRET"
	EnvFolderPath   = "UIPATH_FOLDER_PATH"
	EnvQueueName    = "UIPATH_QUEUE_NAME"
)

// Config is everything the client needs. String and GoString redact ClientSecret, so
// printing a Config with any fmt verb never shows it, and the `json:"-"` keeps it out
// of json.Marshal and of any structured logger that encodes a value by reflection
// rather than through fmt.
type Config struct {
	CloudURL     string `json:"cloudUrl"`   // scheme and host only, e.g. https://cloud.uipath.com
	Org          string `json:"org"`        // organization name as it appears in the URL
	Tenant       string `json:"tenant"`     // tenant name, e.g. DefaultTenant
	ClientID     string `json:"clientId"`   // External Application client id
	ClientSecret string `json:"-"`          // External Application client secret: never serialized
	FolderPath   string `json:"folderPath"` // Orchestrator folder, sent as X-UIPATH-FolderPath
	QueueName    string `json:"queueName"`  // queue the items are added to
}

// LoadConfig reads the configuration through lookup (os.LookupEnv in the CLI). A
// variable that is unset or empty is missing, and the error names every missing one.
func LoadConfig(lookup func(string) (string, bool)) (Config, error) {
	var missing []string
	get := func(name string) string {
		value, ok := lookup(name)
		if !ok || value == "" {
			missing = append(missing, name)
		}
		return value
	}
	cfg := Config{
		CloudURL:     get(EnvCloudURL),
		Org:          get(EnvOrg),
		Tenant:       get(EnvTenant),
		ClientID:     get(EnvClientID),
		ClientSecret: get(EnvClientSecret),
		FolderPath:   get(EnvFolderPath),
		QueueName:    get(EnvQueueName),
	}
	if len(missing) > 0 {
		return Config{}, fmt.Errorf("missing required environment variable(s): %s", strings.Join(missing, ", "))
	}
	if err := cfg.Validate(); err != nil {
		return Config{}, err
	}
	return cfg, nil
}

// Validate reports the first value that cannot work, naming its environment variable.
// Error strings never contain a value, so a misplaced secret is not echoed.
func (c Config) Validate() error {
	fields := []struct{ name, value string }{
		{EnvCloudURL, c.CloudURL},
		{EnvOrg, c.Org},
		{EnvTenant, c.Tenant},
		{EnvClientID, c.ClientID},
		{EnvClientSecret, c.ClientSecret},
		{EnvFolderPath, c.FolderPath},
		{EnvQueueName, c.QueueName},
	}
	for _, f := range fields {
		if f.value == "" {
			return fmt.Errorf("%s is empty", f.name)
		}
		if isPlaceholder(f.value) {
			return fmt.Errorf("%s still holds a placeholder from .env.example", f.name)
		}
	}
	u, err := url.Parse(c.CloudURL)
	if err != nil || u.Scheme != "https" || u.Host == "" || u.User != nil ||
		(u.Path != "" && u.Path != "/") || u.RawQuery != "" || u.Fragment != "" {
		return fmt.Errorf("%s must be https://<host> with no path, e.g. https://cloud.uipath.com", EnvCloudURL)
	}
	// Org and Tenant become path segments. An allowlist rather than a deny list: '..'
	// and '\' pass every deny list worth writing, url.Parse does not normalise dot
	// segments, and a server that does would be asked for a path nobody wrote.
	for _, f := range fields[1:3] {
		if err := validatePathSegment(f.name, f.value); err != nil {
			return err
		}
	}
	for i := 0; i < len(c.FolderPath); i++ {
		if b := c.FolderPath[i]; b < 0x20 || b > 0x7e {
			return fmt.Errorf("%s must be printable ASCII (it is sent as an HTTP header)", EnvFolderPath)
		}
	}
	return nil
}

// String prints the configuration with ClientSecret redacted.
func (c Config) String() string {
	secret := `""`
	if c.ClientSecret != "" {
		secret = redacted
	}
	return fmt.Sprintf("Config{CloudURL:%q Org:%q Tenant:%q ClientID:%q ClientSecret:%s FolderPath:%q QueueName:%q}",
		c.CloudURL, c.Org, c.Tenant, c.ClientID, secret, c.FolderPath, c.QueueName)
}

// GoString keeps %#v from printing ClientSecret.
func (c Config) GoString() string {
	return "orchestrator." + c.String()
}

func (c Config) cloudBase() string {
	return strings.TrimSuffix(c.CloudURL, "/")
}

func (c Config) orchestratorBase() string {
	return c.cloudBase() + "/" + url.PathEscape(c.Org) + "/" + url.PathEscape(c.Tenant) + "/orchestrator_/"
}

func (c Config) tokenURL() string {
	return c.cloudBase() + "/" + url.PathEscape(c.Org) + "/identity_/connect/token"
}

// validatePathSegment accepts letters, digits, '.', '_' and '-', and refuses "." and
// ".." outright: they are legal under that allowlist and are the two that would climb
// the URL rather than name something in it.
func validatePathSegment(name, value string) error {
	if value == "." || value == ".." {
		return fmt.Errorf("%s must name something, not a path step (%q)", name, value)
	}
	for _, r := range value {
		switch {
		case r >= 'a' && r <= 'z', r >= 'A' && r <= 'Z', r >= '0' && r <= '9':
		case r == '.' || r == '_' || r == '-':
		default:
			return fmt.Errorf("%s must hold only letters, digits, '.', '_' and '-'", name)
		}
	}
	return nil
}

// isPlaceholder recognises the {org} and <client-secret> shapes used in .env.example.
func isPlaceholder(value string) bool {
	return (strings.HasPrefix(value, "{") && strings.HasSuffix(value, "}")) ||
		(strings.HasPrefix(value, "<") && strings.HasSuffix(value, ">"))
}
