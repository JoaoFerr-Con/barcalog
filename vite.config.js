import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// BarcaLog — Central de Operações Portuárias
// Build estático (dist/) pronto pra deploy na Vercel.
export default defineConfig({
  plugins: [react()],
  build: {
    outDir: "dist",
    sourcemap: false
  }
});
