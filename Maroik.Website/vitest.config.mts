import { defineConfig } from "vitest/config";
import { fileURLToPath } from "node:url";

// Tests for the custom client scripts. They run the COMPILED wwwroot/**/site.js
// (not the .ts) inside jsdom against a minimal DOM fixture — see
// TypeScripts.Tests/_common/harness.ts. Run `npm run build` first (or `npm test`
// which the CI wires after the build).
export default defineConfig({
  resolve: {
    alias: {
      // `@tests/…` == `TypeScripts.Tests/…`; keeps the helper-file placement
      // (which mirrors wwwroot/{role}/custom/{Feature}/{Page}/js/) readable from
      // any depth. Kept in sync with TypeScripts.Tests/tsconfig.json `paths`.
      "@tests": fileURLToPath(new URL("./TypeScripts.Tests", import.meta.url)),
    },
  },
  test: {
    environment: "jsdom",
    include: ["TypeScripts.Tests/**/*.test.ts"],
    setupFiles: ["TypeScripts.Tests/_common/vitest.setup.ts"],
    globals: true,
    restoreMocks: true,
    unstubGlobals: true,
    // The Calendar smoke tests eval + fake-timer-flush 2,700-line scripts inside jsdom;
    // on a machine also running `dotnet build` the default 5s is occasionally tight.
    testTimeout: 20000,
    hookTimeout: 20000,
  },
});
