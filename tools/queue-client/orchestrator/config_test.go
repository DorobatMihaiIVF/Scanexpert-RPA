package orchestrator

import (
	"encoding/json"
	"fmt"
	"strings"
	"testing"
)

// envLookup turns a map into the os.LookupEnv shape LoadConfig takes.
func envLookup(env map[string]string) func(string) (string, bool) {
	return func(name string) (string, bool) {
		value, ok := env[name]
		return value, ok
	}
}

func completeEnv() map[string]string {
	return map[string]string{
		EnvCloudURL:     "https://cloud.uipath.com",
		EnvOrg:          testOrg,
		EnvTenant:       testTenant,
		EnvClientID:     "client-id",
		EnvClientSecret: testSecret,
		EnvFolderPath:   "PixelData",
		EnvQueueName:    "PixelData_Programari",
	}
}

func TestLoadConfigReadsEveryVariable(t *testing.T) {
	cfg, err := LoadConfig(envLookup(completeEnv()))
	if err != nil {
		t.Fatalf("LoadConfig: %v", err)
	}
	if cfg.QueueName != "PixelData_Programari" || cfg.FolderPath != "PixelData" {
		t.Errorf("cfg = %s, want the values from the environment", cfg)
	}
}

func TestLoadConfigNamesEveryMissingVariable(t *testing.T) {
	for _, name := range []string{
		EnvCloudURL, EnvOrg, EnvTenant, EnvClientID, EnvClientSecret, EnvFolderPath, EnvQueueName,
	} {
		t.Run(name, func(t *testing.T) {
			env := completeEnv()
			delete(env, name)
			_, err := LoadConfig(envLookup(env))
			if err == nil {
				t.Fatalf("LoadConfig: want an error when %s is unset, got none", name)
			}
			if !strings.Contains(err.Error(), name) {
				t.Errorf("error = %q, want it to name %s", err, name)
			}
		})
	}

	t.Run("an empty value counts as missing", func(t *testing.T) {
		env := completeEnv()
		env[EnvQueueName] = ""
		_, err := LoadConfig(envLookup(env))
		if err == nil || !strings.Contains(err.Error(), EnvQueueName) {
			t.Fatalf("error = %v, want it to name %s", err, EnvQueueName)
		}
	})

	t.Run("all of them at once", func(t *testing.T) {
		_, err := LoadConfig(envLookup(nil))
		if err == nil {
			t.Fatal("LoadConfig: want an error with an empty environment, got none")
		}
		for _, name := range []string{
			EnvCloudURL, EnvOrg, EnvTenant, EnvClientID, EnvClientSecret, EnvFolderPath, EnvQueueName,
		} {
			if !strings.Contains(err.Error(), name) {
				t.Errorf("error = %q, want it to name %s", err, name)
			}
		}
	})
}

func TestLoadConfigRefusesPlaceholdersAndBadValues(t *testing.T) {
	cases := map[string]map[string]string{
		"a placeholder org":            {EnvOrg: "{org}"},
		"a placeholder secret":         {EnvClientSecret: "<client-secret>"},
		"http instead of https":        {EnvCloudURL: "http://cloud.uipath.com"},
		"a cloud URL with a path":      {EnvCloudURL: "https://cloud.uipath.com/acme"},
		"an org with a slash":          {EnvOrg: "acme/sub"},
		"a tenant with a space":        {EnvTenant: "Default Tenant"},
		"a folder path with a newline": {EnvFolderPath: "PixelData\n"},
	}
	for name, override := range cases {
		t.Run(name, func(t *testing.T) {
			env := completeEnv()
			for key, value := range override {
				env[key] = value
			}
			if _, err := LoadConfig(envLookup(env)); err == nil {
				t.Fatal("LoadConfig: want an error, got none")
			}
		})
	}
}

func TestConfigNeverPrintsTheSecret(t *testing.T) {
	cfg, err := LoadConfig(envLookup(completeEnv()))
	if err != nil {
		t.Fatalf("LoadConfig: %v", err)
	}
	for _, printed := range []string{
		cfg.String(),
		cfg.GoString(),
		fmt.Sprintf("%v", cfg),
		fmt.Sprintf("%s", cfg),
		fmt.Sprintf("%#v", cfg),
	} {
		assertRedacted(t, "printed config", printed)
		if !strings.Contains(printed, redacted) {
			t.Errorf("printed config does not mark the secret as %s: %s", redacted, printed)
		}
	}
}

func TestValidateErrorsNeverQuoteAValue(t *testing.T) {
	env := completeEnv()
	env[EnvClientSecret] = "<client-secret>"
	_, err := LoadConfig(envLookup(env))
	if err == nil {
		t.Fatal("LoadConfig: want an error for a placeholder secret, got none")
	}
	if strings.Contains(err.Error(), "<client-secret>") {
		t.Errorf("error = %q, want it to name the variable without quoting the value", err)
	}
}

// String() covers fmt, but a structured logger encodes by reflection and would have
// reached the secret straight through the exported field.
func TestConfigIsNeverSerializedWithTheSecret(t *testing.T) {
	cfg, err := LoadConfig(envLookup(completeEnv()))
	if err != nil {
		t.Fatalf("LoadConfig: %v", err)
	}
	encoded, err := json.Marshal(cfg)
	if err != nil {
		t.Fatalf("json.Marshal: %v", err)
	}
	assertRedacted(t, "the marshalled config", string(encoded))
	if strings.Contains(string(encoded), "clientSecret") {
		t.Errorf("the marshalled config names the secret field: %s", encoded)
	}
	if !strings.Contains(string(encoded), testOrg) {
		t.Errorf("the marshalled config lost the fields that are safe to keep: %s", encoded)
	}
}

// Org and Tenant become URL path segments, so an allowlist rather than a deny list.
func TestOrgAndTenantMustBeOnePlainPathSegment(t *testing.T) {
	refused := []string{"..", ".", "a/b", `a\b`, "a b", "a?b", "a#b", "a%b", "../other", "a\nb", "ünicode"}
	for _, value := range refused {
		for _, name := range []string{EnvOrg, EnvTenant} {
			env := completeEnv()
			env[name] = value
			if _, err := LoadConfig(envLookup(env)); err == nil {
				t.Errorf("LoadConfig: %s = %q was accepted, want an error", name, value)
			}
		}
	}

	for _, value := range []string{"acme", "Acme-Corp_1", "tenant.two", "DefaultTenant", "a"} {
		env := completeEnv()
		env[EnvOrg] = value
		if _, err := LoadConfig(envLookup(env)); err != nil {
			t.Errorf("LoadConfig: %s = %q was refused: %v", EnvOrg, value, err)
		}
	}
}

func TestDotSegmentsCannotClimbTheURL(t *testing.T) {
	env := completeEnv()
	env[EnvOrg] = ".."
	_, err := LoadConfig(envLookup(env))
	if err == nil {
		t.Fatal("LoadConfig: want an error for an org of '..', got none")
	}
	if !strings.Contains(err.Error(), EnvOrg) {
		t.Errorf("error = %q, want it to name %s", err, EnvOrg)
	}
}
