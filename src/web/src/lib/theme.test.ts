import assert from 'node:assert/strict'
import { test } from 'node:test'
import { toTheme } from './theme.ts'

test('saved palette', () => {
  assert.equal(toTheme('ivory'), 'ivory')
  assert.equal(toTheme('noir'), 'noir')
  assert.equal(toTheme(null), 'emerald')
  assert.equal(toTheme('something else'), 'emerald')
  // Choices saved by the earlier Light/Dark setting.
  assert.equal(toTheme('light'), 'emerald')
  assert.equal(toTheme('dark'), 'noir')
})
