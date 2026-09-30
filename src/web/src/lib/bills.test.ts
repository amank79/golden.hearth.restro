import assert from 'node:assert/strict'
import { test } from 'node:test'
import { plural } from './bills.ts'

test('plural', () => {
  assert.equal(plural(1, 'bill'), '1 bill')
  assert.equal(plural(0, 'bill'), '0 bills')
  assert.equal(plural(12, 'item'), '12 items')
  assert.equal(plural(2, 'dish', 'dishes'), '2 dishes')
  assert.equal(plural(1, 'dish', 'dishes'), '1 dish')
})
