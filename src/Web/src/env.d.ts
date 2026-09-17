/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Address of the Gateway, injected by Aspire (or the local default when running Vite on its own). */
  readonly VITE_GATEWAY_URL?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
