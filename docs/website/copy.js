"use strict";

document.getElementById("copy-install")?.addEventListener("click", async function () {
  const command = document.querySelector(".install-command code");
  const status = document.getElementById("copy-status");
  if (!command || !status) return;
  try {
    await navigator.clipboard.writeText(command.textContent);
    status.textContent = "Command copied.";
  } catch (_) {
    const selection = window.getSelection();
    const range = document.createRange();
    range.selectNodeContents(command);
    selection.removeAllRanges();
    selection.addRange(range);
    status.textContent = "Copy unavailable. The command is selected; copy it manually.";
  }
});
