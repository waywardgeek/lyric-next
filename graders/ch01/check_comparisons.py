#!/usr/bin/env python3
"""Retained grader self-check; not a compiler conformance result."""
import json
from grade import check_diagnostic, expected_events, same_json


def rejects(action):
    try:
        action()
    except (AssertionError, ValueError):
        return
    raise AssertionError('Comparator accepted a malformed diagnostic')


def main():
    expected = expected_events('')
    assert same_json(expected, expected)
    assert same_json([dict(kind='end', byteOffset=0.0, line=1, column=1)], expected)
    for field, value in [('byteOffset', False), ('line', True), ('column', True)]:
        wrong = [dict(expected[0], **{field: value})]
        assert not same_json(wrong, expected), field
    assert not same_json([dict(expected[0], extra=0)], expected)
    assert not same_json([], expected)
    path = 'quote"+é.ly'
    suffix = ':1:1: error LYC001: Malformed UTF-8 sequence. (byte 0)\n'
    for quote in [json.dumps(path), json.dumps(path, ensure_ascii=False),
                  json.dumps(path).replace('+', '\\u002B')]:
        check_diagnostic(quote + suffix, path, suffix)
    rejects(lambda: check_diagnostic(json.dumps('wrong.ly') + suffix, path, suffix))
    rejects(lambda: check_diagnostic(json.dumps(path) + suffix + 'extra\n', path, suffix))
    rejects(lambda: check_diagnostic('"bad\npath"' + suffix, 'bad\npath', suffix))
    rejects(lambda: check_diagnostic('"bad\x85path"' + suffix, 'bad\x85path', suffix))
    print('Grader comparison self-check passed')


if __name__ == '__main__':
    main()
