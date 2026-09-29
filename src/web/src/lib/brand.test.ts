import assert from 'node:assert/strict'
import { test } from 'node:test'
import { brandParts } from './brand.ts'

test('wordmark lines', () => {
  assert.deepEqual(brandParts('The Golden Hearth Restaurant'), { main: 'The Golden Hearth', sub: 'Restaurant' })
  assert.deepEqual(brandParts('Sharma Dhaba'), { main: 'Sharma', sub: 'Dhaba' })
  assert.deepEqual(brandParts('  Golden   Hearth  '), { main: 'Golden Hearth', sub: '' })
  assert.deepEqual(brandParts('Restaurant'), { main: 'Restaurant', sub: '' })
  assert.deepEqual(brandParts(''), { main: 'Restaurant POS', sub: '' })
  assert.deepEqual(brandParts(undefined), { main: 'Restaurant POS', sub: '' })
})
