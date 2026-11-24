const { describe, it } = require('node:test');
const assert = require('node:assert/strict');
const { parseArguments } = require('../out/argumentParser');

describe('parseArguments', () => {
  it('splits arguments on whitespace', () => {
    assert.deepEqual(parseArguments('alpha beta\tgamma'), ['alpha', 'beta', 'gamma']);
  });

  it('preserves whitespace inside quoted arguments', () => {
    assert.deepEqual(parseArguments('one "two three" \'four five\''), ['one', 'two three', 'four five']);
  });

  it('supports escaped characters outside quotes', () => {
    assert.deepEqual(parseArguments('one\\ two three\\\tfour'), ['one two', 'three\tfour']);
  });

  it('supports escaped characters inside double quotes', () => {
    assert.deepEqual(parseArguments('"say \\"hello\\""'), ['say "hello"']);
  });

  it('preserves backslashes inside single quotes', () => {
    assert.deepEqual(parseArguments("'a\\b'"), ['a\\b']);
  });

  it('preserves empty quoted arguments', () => {
    assert.deepEqual(parseArguments('one "" \'\''), ['one', '', '']);
  });

  it('rejects a trailing escape', () => {
    assert.throws(() => parseArguments('one\\'), /incomplete escape/);
  });

  it('rejects an unclosed quote', () => {
    assert.throws(() => parseArguments('"unfinished'), /Unclosed/);
  });
});
