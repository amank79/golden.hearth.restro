// Run with `npm test` (Node's built-in test runner; no extra packages).
import assert from 'node:assert/strict'
import { test } from 'node:test'
import { formatPhone, isPhoneText } from './customers.ts'

test('phone numbers are shown as 5 + 5 digits', () => {
  assert.equal(formatPhone('9825012345'), '98250 12345')
  assert.equal(formatPhone('12345'), '12345')
})

test('search text is a phone number when it has only digits, spaces, + or -', () => {
  assert.equal(isPhoneText('98250'), true)
  assert.equal(isPhoneText('+91 98250-12345'), true)
  assert.equal(isPhoneText('Ramesh'), false)
  assert.equal(isPhoneText('Table 4'), false)
  assert.equal(isPhoneText('  '), false)
})
