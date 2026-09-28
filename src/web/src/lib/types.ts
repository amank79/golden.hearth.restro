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
