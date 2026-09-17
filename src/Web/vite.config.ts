import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// The port is fixed instead of "whatever is free" because the Gateway only allows the configured
// origins through CORS (see the Cors section of the Gateway settings). Aspire passes PORT when it
// starts this app; running "npm run dev" by hand falls back to the Vite default.
const port = Number(process.env.PORT ?? 5173)

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: { port, strictPort: true },
  preview: { port: 4173, strictPort: true },
})
