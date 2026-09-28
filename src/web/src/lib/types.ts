export type TaxMode = 'Regular' | 'Composition'

export interface Settings {
  name: string
  address: string
  phone: string
  gstin: string
  fssaiNo: string
  taxMode: TaxMode
  gstRateBp: number
  billFooter: string
}

export type FoodType = 'Veg' | 'NonVeg' | 'Egg'

export interface Category {
  id: number
  name: string
  sortOrder: number
  isActive: boolean
}

export interface Variant {
  id: number
  name: string
  pricePaise: number
  sortOrder: number
}

export interface MenuItem {
  id: number
  categoryId: number
  name: string
  shortCode: string | null
  description: string | null
  foodType: FoodType
  gstRateBp: number | null
  isAvailable: boolean
  isActive: boolean
  sortOrder: number
  variants: Variant[]
}

export interface Menu {
  categories: Category[]
  items: MenuItem[]
}

export interface MenuItemInput {
  categoryId: number
  name: string
  shortCode: string | null
  description: string | null
  foodType: FoodType
  gstRateBp: number | null
  isAvailable: boolean
  sortOrder: number | null
  variants: { id: number | null; name: string | null; pricePaise: number }[]
}
