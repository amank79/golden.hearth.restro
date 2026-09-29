// Run with `npm test` (Node's built-in test runner; no extra packages).
import assert from 'node:assert/strict'
import { test } from 'node:test'
import { paiseToInput, parsePercent, parseRupees, percent, rupees, rupeesShort } from './money.ts'

test('rupees uses Indian grouping and 2 decimals', () => {
  assert.equal(rupees(123450), '₹1,234.50')
  assert.equal(rupees(10000000), '₹1,00,000.00')
  assert.equal(rupees(123456789), '₹12,34,567.89')
  assert.equal(rupees(5), '₹0.05')
})

test('rupeesShort drops .00', () => {
  assert.equal(rupeesShort(29000), '₹290')
  assert.equal(rupeesShort(29050), '₹290.50')
})

test('parseRupees reads typed amounts as exact paise', () => {
  assert.equal(parseRupees('120'), 12000)
  assert.equal(parseRupees('1,234.5'), 123450)
  assert.equal(parseRupees('₹ 99.99'), 9999)
  assert.equal(parseRupees('1.005'), null) // at most 2 decimals
  assert.equal(parseRupees('0.1'), 10)
  assert.equal(parseRupees('abc'), null)
  assert.equal(parseRupees('-5'), null)
  assert.equal(parseRupees(''), null)
})

test('paiseToInput round-trips', () => {
  for (const p of [0, 5, 10, 12000, 12050, 123456]) assert.equal(parseRupees(paiseToInput(p)), p)
})

test('percent and parsePercent', () => {
  assert.equal(percent(500), '5')
  assert.equal(percent(250), '2.5')
  assert.equal(percent(1250), '12.5')
  assert.equal(parsePercent('2.5'), 250)
  assert.equal(parsePercent('12.50%'), 1250)
  assert.equal(parsePercent('x'), null)
})
