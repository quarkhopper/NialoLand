---
description: "Use when writing TypeScript, JavaScript, Node.js apps, React frontends, or browser experiments. Covers modern TS/JS patterns, stream-friendly UIs, and Vite/Node setup."
applyTo: ["**/*.ts", "**/*.tsx", "**/*.js", "**/*.jsx", "**/package.json", "**/tsconfig.json"]
---

# TypeScript / JavaScript Guidelines for NialoLand

## Project Setup

### Node.js / CLI app
```bash
npm init -y
npm install -D typescript ts-node @types/node
npx tsc --init
```

### Web frontend (Vite — fast and stream-friendly)
```bash
npm create vite@latest my-app -- --template react-ts
cd my-app && npm install
npm run dev   # instant hot-reload, great on stream
```

### Node API (Fastify — fast, minimal)
```bash
npm install fastify
npm install -D typescript @types/node ts-node
```

## TypeScript Config

Use strict mode — it catches bugs live on stream:

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "NodeNext",
    "moduleResolution": "NodeNext",
    "strict": true,
    "outDir": "dist",
    "esModuleInterop": true
  }
}
```

## Terminal Output (Node.js)

Use **chalk** or **kleur** for colors — lightweight and readable:

```bash
npm install chalk    # ESM-native, use with type: module
```

```typescript
import chalk from 'chalk';
console.log(chalk.green('Done!'));
console.log(chalk.red.bold('Error:'), message);
```

For progress bars: `cli-progress` package.

## Web UI Patterns

React + Tailwind CSS = fast beautiful UIs on stream:

```bash
npm install -D tailwindcss postcss autoprefixer
npx tailwindcss init -p
```

Use the shadcn/ui component library for polished components fast:
```bash
npx shadcn@latest init
npx shadcn@latest add button card input
```

## API Routes (Fastify)

```typescript
import Fastify from 'fastify';

const app = Fastify({ logger: true });

app.get('/health', async () => ({ status: 'ok' }));

app.get('/', async () => 'NialoLand API is live!');

await app.listen({ port: Number(process.env.PORT ?? 3000), host: '0.0.0.0' });
```

Always bind to `0.0.0.0` and use `process.env.PORT` — Railway requires both.

## Configuration & Secrets

```typescript
// Good — env vars with a clear error
const apiKey = process.env.MY_API_KEY;
if (!apiKey) throw new Error('MY_API_KEY environment variable is required');

// Never
const apiKey = 'sk-abc123...';
```

Use `dotenv` locally:
```bash
npm install dotenv
```
```typescript
import 'dotenv/config'; // at the very top of entry point
```

## Naming & Style

- `camelCase` for variables and functions
- `PascalCase` for classes and React components
- `SCREAMING_SNAKE_CASE` for constants
- `async/await` over `.then()` chains — easier to read on stream
- Prefer `const` over `let`; avoid `var`
- Arrow functions for callbacks; named functions for top-level

## Key Packages

| Package | Purpose |
|---------|---------|
| `chalk` | Terminal colors |
| `fastify` | Lightweight web framework |
| `zod` | Runtime type validation |
| `axios` / `ky` | HTTP client |
| `dotenv` | Local env vars |
| `vite` | Frontend bundler |
| `tailwindcss` | Utility-first CSS |
