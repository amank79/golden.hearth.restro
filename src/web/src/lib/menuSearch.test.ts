import assert from 'node:assert/strict'
import { test } from 'node:test'
import { dishName, searchItems, searchRank } from './menuSearch.ts'

// Same cases as MenuSearchTests.cs, so the counter and the server agree.
test('ranks like the server', () => {
  assert.equal(searchRank('Paneer Butter Masala', 'PBM', 'pbm'), 0)
  assert.equal(searchRank('Paneer Butter Masala', 'PBM', 'PB'), 1)
  assert.equal(searchRank('Paneer Butter Masala', 'PBM', 'paneer b'), 2)
  assert.equal(searchRank('Paneer Butter Masala', 'PBM', 'butt'), 3)
  assert.equal(searchRank('Paneer Butter Masala', null, 'pbm'), 4)
  assert.equal(searchRank('Paneer Butter Masala', 'PBM', 'ter mas'), 5)
  assert.equal(searchRank('Dal Makhani', 'DM', 'paneer'), null)
  assert.equal(searchRank('Dal Makhani', null, '  '), 0)
})

test('searchItems orders best match first', () => {
  const items = [
    { name: 'Butter Naan', shortCode: 'BN' },
    { name: 'Paneer Butter Masala', shortCode: 'PBM' },
    { name: 'Kadai Paneer', shortCode: 'KP' },
  ]
  assert.deepEqual(searchItems(items, 'paneer').map((i) => i.name), ['Paneer Butter Masala', 'Kadai Paneer'])
  assert.deepEqual(searchItems(items, 'bn').map((i) => i.name), ['Butter Naan'])
})

test('dishName hides Regular', () => {
  assert.equal(dishName('Butter Naan', 'Regular'), 'Butter Naan')
  assert.equal(dishName('Dal Makhani', 'Half'), 'Dal Makhani (Half)')
})
