import { defineConfig } from 'orval';

// Generates typed TanStack Query hooks from the OpenAPI document the backend writes on build.
// Run `npm run api:generate` after changing any API endpoint; CI fails if the output is stale.
export default defineConfig({
  schoolms: {
    input: { target: '../backend/openapi/schoolms-api.json' },
    output: {
      mode: 'tags-split',
      target: 'src/api/generated/endpoints',
      schemas: 'src/api/generated/model',
      client: 'react-query',
      httpClient: 'fetch',
      clean: true,
      override: {
        mutator: { path: 'src/api/http.ts', name: 'http' },
        fetch: { includeHttpResponseReturnType: false },
      },
    },
  },
});
