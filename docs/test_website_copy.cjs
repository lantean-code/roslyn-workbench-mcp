"use strict";

const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

async function checkCopy(clipboardWorks) {
  const template = fs.readFileSync(path.join(__dirname, "website/index.html"), "utf8");
  const visible = template.match(/<code>(dnx [^<]+)<\/code>/)[1].replace("{{VERSION}}", "1.23.456");
  let handler;
  let copied;
  let selected;
  const status = { textContent: "" };
  const command = { textContent: visible };
  const document = {
    getElementById: id => id === "copy-status" ? status : {addEventListener: (_, callback) => {handler = callback;}},
    querySelector: () => command,
    createRange: () => ({selectNodeContents: node => { selected = node.textContent; }})
  };
  const context = {
    document,
    navigator: {clipboard: {writeText: async value => {
      if (!clipboardWorks) throw new Error("Clipboard denied");
      copied = value;
    }}},
    window: {getSelection: () => ({removeAllRanges() {}, addRange() {}})}
  };
  vm.runInNewContext(fs.readFileSync(path.join(__dirname, "website/copy.js"), "utf8"), context);
  await handler();
  if (clipboardWorks) {
    assert.equal(copied, visible);
    assert.equal(status.textContent, "Command copied.");
  } else {
    assert.equal(selected, visible);
    assert.match(status.textContent, /selected/);
  }
}

(async () => {
  await checkCopy(true);
  await checkCopy(false);
  console.log("Command copying: exact visible bytes and denied-clipboard fallback passed.");
})().catch(error => {console.error(error); process.exitCode = 1;});
