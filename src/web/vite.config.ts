import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// Dev: `npm run dev` on :5173 forwards /api to the .NET API on :5080.
// Build: output goes into the API's wwwroot, which serves the app in production.
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: { '/api': 'http://localhost:5080' },
  },
  build: {
    outDir: '../RestaurantPos.Api/wwwroot',
    emptyOutDir: true,
  },
})
