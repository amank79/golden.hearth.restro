import assert from 'node:assert/strict'
import { test } from 'node:test'
import { toTheme } from './theme.ts'

test('saved palette', () => {
  assert.equal(toTheme('navy'), 'navy')
  assert.equal(toTheme('burgundy'), 'burgundy')
  assert.equal(toTheme('midnight'), 'midnight')
  assert.equal(toTheme(null), 'navy')
  assert.equal(toTheme('something else'), 'navy')
  // Choices saved by design A (Emerald / Ivory / Noir) and the earlier Light/Dark setting.
  assert.equal(toTheme('emerald'), 'navy')
  assert.equal(toTheme('ivory'), 'navy')
  assert.equal(toTheme('noir'), 'midnight')
  assert.equal(toTheme('light'), 'navy')
  assert.equal(toTheme('dark'), 'midnight')
})
