// tools/ui2unity.js
const { readFileSync, writeFileSync } = require("fs");
const cheerio = require("cheerio");

/** HTML 标签到 UXML 元素的最小映射 */
const tagMap = {
  div: "VisualElement",
  span: "Label",
  p: "Label",
  h1: "Label",
  h2: "Label",
  h3: "Label",
  button: "Button",
  img: "Image",
  ul: "VisualElement",
  li: "Label",
  input: "TextField",
  textarea: "TextField",
};

function parseNode($, node) {
  if (node.type === "text") {
    const t = (node.data || "").trim();
    return t ? { tag: "Label", attrs: { text: t }, children: [] } : null;
  }
  if (node.type !== "tag") return null;

  const htmlTag = node.name.toLowerCase();
  const tagMap = {
    div: "VisualElement",
    span: "Label",
    p: "Label",
    h1: "Label",
    h2: "Label",
    h3: "Label",
    button: "Button",
    img: "Image",
    ul: "VisualElement",
    li: "Label",
    input: "TextField",
    textarea: "TextField",
  };
  const tag = tagMap[htmlTag] || "VisualElement";
  const attrs = {};
  const name = $(node).attr("id");
  const cls  = ($(node).attr("class") || "").trim();
  if (name) attrs.name = name;
  if (cls)  attrs.class = cls;

  // —— 这些控件自己带 text/属性，不要继续遍历子文本 ——
  const isLeafWithText = tag === "Label" || tag === "Button" || tag === "TextField";

  if (tag === "Label") {
    const txt = $(node).text().trim();
    if (txt) attrs.text = txt;
  }
  if (tag === "Button") {
    const txt = $(node).text().trim();
    attrs.text = txt || "Button";
  }
  if (tag === "TextField") {
    if (htmlTag === "textarea") attrs.multiline = "true";
    const placeholder = $(node).attr("placeholder");
    if (placeholder) attrs.label = placeholder;
  }
  if (tag === "Image") {
    const dataSrc = $(node).attr("data-src");
    if (dataSrc && !attrs.name) attrs.name = `Img_${dataSrc}`;
  }
  if (htmlTag === "ul" || htmlTag === "ol") {
    attrs["style.flex-direction"] = "column";
  }

  const children = [];
  if (!isLeafWithText) {                  // 关键：这些控件不再下钻
    for (const c of node.children || []) {
      const child = parseNode($, c);
      if (child) children.push(child);
    }
  }
  return { tag, attrs, children };
}

function emitUXML(node, indent = "  ") {
  const attrs = Object.entries(node.attrs || {})
    .map(([k, v]) => `${k}="${String(v).replace(/"/g, "&quot;")}"`)
    .join(" ");
  const open = attrs ? `<${node.tag} ${attrs}>` : `<${node.tag}>`;
  if (!node.children || node.children.length === 0) return `${open}</${node.tag}>`;
  const body = node.children.map(c => indent + emitUXML(c, indent)).join("\n");
  return `${open}\n${body}\n</${node.tag}>`;
}

function buildUXML($, outUxmlPath) {
  const roots = $("body").children().toArray().map(el => parseNode($, el)).filter(Boolean);
  const content = roots.map(c => "  " + emitUXML(c)).join("\n");
  const ussName = outUxmlPath ? outUxmlPath.replace(/^.*\/([^/]+)\.uxml$/,"$1.uss") : null;

  return `<?xml version="1.0" encoding="utf-8"?>
<UXML xmlns="UnityEngine.UIElements">
${ussName ? `  <Style src="${ussName}" />\n` : ""}${content}
</UXML>`;
}

// CLI: node tools/ui2unity.js input.html out.uxml out.uss
const [,, inHtml, outUxml, outUss] = process.argv;
if (!inHtml || !outUxml || !outUss) {
  console.error("Usage: node tools/ui2unity.js input.html out.uxml out.uss");
  process.exit(1);
}
const html = readFileSync(inHtml, "utf8");
const $ = cheerio.load(html);
writeFileSync(outUxml, buildUXML($), "utf8");
writeFileSync(outUss,  buildUSS($), "utf8");
console.log("Wrote:", outUxml, outUss);
