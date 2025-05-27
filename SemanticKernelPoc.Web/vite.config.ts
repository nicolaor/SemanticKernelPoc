import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { readFileSync } from 'fs'
import { resolve } from 'path'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 31337,
    host: true,
    https: {
      key: readFileSync(resolve(__dirname, '../certs/localhost.key')),
      cert: readFileSync(resolve(__dirname, '../certs/localhost.crt'))
    }
  }
})
