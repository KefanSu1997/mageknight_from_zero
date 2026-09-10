#!/usr/bin/env node
// mcp-ark-vision.mjs
// Minimal MCP server that calls Volcengine Ark Chat Completions (vision)

import { readFile } from "node:fs/promises";
import path from "node:path";
import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
  McpError,
  ErrorCode,
} from "@modelcontextprotocol/sdk/types.js";

const ARK_API_KEY = process.env.ARK_API_KEY;
const ARK_BASE_URL = process.env.ARK_BASE_URL || "https://ark.cn-beijing.volces.com/api/v3";
const ARK_MODEL = process.env.ARK_MODEL || "doubao-seed-1-6-vision-250815";

if (!ARK_API_KEY) {
  // IMPORTANT: never log to stdout, MCP uses stdout for JSON-RPC
  console.error("Missing ARK_API_KEY in environment.");
  process.exit(1);
}

const server = new Server(
  {
    name: "ark-vision-mcp",
    version: "0.1.0",
  },
  {
    capabilities: { tools: {} },
  }
);

// ---- Tool registry ----
server.setRequestHandler(ListToolsRequestSchema, async () => ({
  tools: [
    {
      name: "ark_vision.describe_image",
      description:
        "Use Volcengine Ark vision model to describe an image from a URL or local file path.",
      inputSchema: {
        type: "object",
        properties: {
          image: { type: "string", description: "HTTP URL or local file path" },
          prompt: {
            type: "string",
            description: "Optional instruction; default asks to describe the image.",
          },
          model: {
            type: "string",
            description: "Override model id (default: doubao-seed-1-6-vision-250815)",
          },
        },
        required: ["image"],
      },
    },
  ],
}));

// ---- Tool handler ----
server.setRequestHandler(CallToolRequestSchema, async (req) => {
  try {
    if (req.params.name !== "ark_vision.describe_image") {
      throw new McpError(ErrorCode.MethodNotFound, "Unknown tool");
    }

    const args = req.params.arguments ?? {};
    const img = String(args.image || "").trim();
    const prompt =
      (args.prompt && String(args.prompt)) || "请详细描述这张图片的内容与关键信息。";
    const model = String(args.model || ARK_MODEL);

    if (!img) throw new McpError(ErrorCode.InvalidParams, "image is required");

    let imageUrlForArk;
    if (/^https?:\/\//i.test(img)) {
      // Remote image URL
      imageUrlForArk = img;
    } else {
      // Local file -> data URL (base64)
      const abs = path.isAbsolute(img) ? img : path.resolve(process.cwd(), img);
      const data = await readFile(abs);
      const ext = path.extname(abs).toLowerCase().replace(".", "");
      const mime =
        ext === "jpg" || ext === "jpeg"
          ? "image/jpeg"
          : ext === "png"
          ? "image/png"
          : ext === "webp"
          ? "image/webp"
          : ext === "gif"
          ? "image/gif"
          : "application/octet-stream";
      const b64 = data.toString("base64");
      imageUrlForArk = `data:${mime};base64,${b64}`;
    }

    // Ark Chat Completions payload (vision)
    // Supports content array with {type:"image_url", image_url:{url}} and {type:"text", text}
    const payload = {
      model,
      messages: [
        {
          role: "user",
          content: [
            { type: "image_url", image_url: { url: imageUrlForArk } },
            { type: "text", text: prompt },
          ],
        },
      ],
    };

    const res = await fetch(`${ARK_BASE_URL}/chat/completions`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${ARK_API_KEY}`,
      },
      body: JSON.stringify(payload),
    });

    if (!res.ok) {
      const errText = await res.text().catch(() => "");
      throw new McpError(
        ErrorCode.InternalError,
        `Ark HTTP ${res.status}: ${errText || res.statusText}`
      );
    }

    const json = await res.json();

    // Try to extract text in both "string" and "array" content shapes
    let text = "";
    try {
      // OpenAI-like
      text =
        json?.choices?.[0]?.message?.content ??
        "";
    } catch {
      /* noop */
    }
    if (Array.isArray(text)) {
      text = text.map((c) => (typeof c === "string" ? c : c?.text ?? "")).join("\n");
    }
    if (!text || typeof text !== "string") {
      text = JSON.stringify(json);
    }

    return {
      content: [{ type: "text", text }],
    };
  } catch (err) {
    const msg = err?.message || String(err);
    return {
      isError: true,
      content: [{ type: "text", text: `ark_vision.describe_image failed: ${msg}` }],
    };
  }
});

// ---- Start STDIO transport ----
const transport = new StdioServerTransport();
server.connect(transport).catch((e) => {
  console.error("Failed to start MCP server:", e);
  process.exit(1);
});
