#!/usr/bin/env python3
"""Check contracts/examples/ against the v1 queue item schema.

- examples/valid/*.json and examples/invalid/business/*.json must PASS the schema;
- examples/invalid/schema/*.json must FAIL it (the first error is printed);
- every example must be the flat SpecificContent object, never the
  {"itemData": ...} envelope, and must be plain JSON (no duplicate keys, no NaN).

Exit codes: 0 all as expected, 1 at least one mismatch, 2 setup error
(jsonschema missing, a schema missing or invalid, examples/valid missing or empty).
A missing or empty invalid/ folder is reported and tolerated.
"""

import json
import re
import sys
from functools import lru_cache
from pathlib import Path

try:
    from jsonschema import Draft202012Validator, validators
    from jsonschema.exceptions import SchemaError, ValidationError, best_match
except ImportError:
    print("SETUP ERROR: the python package 'jsonschema' is not installed")
    sys.exit(2)

HERE = Path(__file__).resolve().parent
REPO = HERE.parent
SCHEMAS = (
    HERE / "appointment-queue-item.v1.schema.json",
    HERE / "queue-item-output.v1.schema.json",
)
EXAMPLES = HERE / "examples"
# (folder, must pass the schema, folder must exist and hold files)
GROUPS = (
    (EXAMPLES / "valid", True, True),
    (EXAMPLES / "invalid" / "business", True, False),
    (EXAMPLES / "invalid" / "schema", False, False),
)


@lru_cache(maxsize=None)
def ecma_to_python(pattern):
    """JSON Schema patterns are ECMA-262 regexes. Python's `$` also matches just
    before a trailing newline, so "1234567890123\\n" would pass ^[0-9]{13}$ here
    and fail everywhere else. Rewrite each unescaped `$` outside a character
    class to `\\Z`, which is ECMA's `$` without the multiline flag."""
    out = []
    i = 0
    in_class = False
    while i < len(pattern):
        ch = pattern[i]
        if ch == "\\" and i + 1 < len(pattern):
            out.append(pattern[i:i + 2])
            i += 2
            continue
        if in_class:
            if ch == "]":
                in_class = False
        elif ch == "[":
            in_class = True
        elif ch == "$":
            ch = r"\Z"
        out.append(ch)
        i += 1
    return "".join(out)


def ecma_pattern(validator, pattern, instance, schema):
    if validator.is_type(instance, "string") and not re.search(ecma_to_python(pattern), instance):
        yield ValidationError(f"{instance!r} does not match {pattern!r}")


ContractValidator = validators.extend(Draft202012Validator, {"pattern": ecma_pattern})


class DuplicateKeyError(ValueError):
    pass


def reject_duplicates(pairs):
    obj = {}
    for key, value in pairs:
        if key in obj:
            raise DuplicateKeyError(f"duplicate key {key!r}")
        obj[key] = value
    return obj


def reject_constant(name):
    raise ValueError(f"{name} is not valid JSON")


def load_json(path):
    with path.open(encoding="utf-8") as fh:
        return json.load(fh, object_pairs_hook=reject_duplicates, parse_constant=reject_constant)


def compile_patterns(node):
    """check_schema does not compile regexes; do it so a broken pattern is a setup error."""
    if isinstance(node, dict):
        for key, value in node.items():
            if key == "pattern" and isinstance(value, str):
                re.compile(ecma_to_python(value))
            else:
                compile_patterns(value)
    elif isinstance(node, list):
        for item in node:
            compile_patterns(item)


def rel(path):
    return path.relative_to(REPO).as_posix()


def describe(error):
    where = "/".join(str(part) for part in error.absolute_path) or "(root)"
    return f"{where}: {error.message}"


def envelope_problem(doc):
    if not isinstance(doc, dict):
        return "top level is not a JSON object (must be the flat SpecificContent object)"
    if "itemData" in doc:
        return 'file is the {"itemData": ...} envelope; examples hold only the flat SpecificContent object'
    return None


def main():
    try:
        loaded = []
        for path in SCHEMAS:
            schema = load_json(path)
            Draft202012Validator.check_schema(schema)
            compile_patterns(schema)
            loaded.append(schema)
            print(f"SCHEMA    {rel(path)}: valid draft 2020-12 schema")
    except (OSError, ValueError, SchemaError, re.error) as exc:
        print(f"SETUP ERROR: {exc}")
        return 2

    validator = ContractValidator(loaded[0])
    checked = 0
    mismatches = 0
    for folder, must_pass, required in GROUPS:
        files = sorted(folder.glob("*.json")) if folder.is_dir() else []
        if not files:
            if required:
                print(f"SETUP ERROR: {rel(folder)} is missing or holds no .json files")
                return 2
            print(f"NOTE      {rel(folder)} is missing or empty; nothing checked there")
            continue
        for path in files:
            checked += 1
            name = rel(path)
            try:
                doc = load_json(path)
            except (OSError, ValueError) as exc:
                mismatches += 1
                print(f"MISMATCH  {name}: not readable as plain JSON: {exc}")
                continue
            problem = envelope_problem(doc)
            if problem:
                mismatches += 1
                print(f"MISMATCH  {name}: {problem}")
                continue
            first = best_match(validator.iter_errors(doc))
            if must_pass and first is None:
                print(f"OK        {name}")
            elif must_pass:
                mismatches += 1
                print(f"MISMATCH  {name}: expected to pass the schema, first error: {describe(first)}")
            elif first is not None:
                print(f"OK        {name}: fails as expected: {describe(first)}")
            else:
                mismatches += 1
                print(f"MISMATCH  {name}: expected to fail the schema, but it passes")

    print(f"Checked {checked} example file(s): {mismatches} mismatch(es).")
    return 1 if mismatches else 0


if __name__ == "__main__":
    sys.exit(main())
