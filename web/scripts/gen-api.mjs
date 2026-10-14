import { execFile } from "node:child_process";
import { promisify } from "node:util";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const execFileAsync = promisify(execFile);

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const baseUrl = (process.env.VITE_API_URL ?? "http://localhost:8080").replace(/\/+$/, "");
const specUrl = `${baseUrl}/swagger/v1/swagger.json`;
const outFile = resolve(root, "src/lib/api/schema.d.ts");
const cli = resolve(root, "node_modules/openapi-typescript/bin/cli.js");

console.log(`Generating typed client from ${specUrl}`);
await execFileAsync(process.execPath, [cli, specUrl, "-o", outFile]);
console.log(`Wrote ${outFile}`);