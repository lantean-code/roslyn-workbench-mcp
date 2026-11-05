"use strict";

(() => {
  const notice = document.querySelector("[data-rw-outdated-docs]");
  if (notice) {
    const key = "rw-docs-dismissed:" + notice.getAttribute("data-rw-outdated-docs");
    try {
      notice.hidden = sessionStorage.getItem(key) === "true";
    } catch {
      // The notice still works when browser storage is unavailable.
    }
    const dismiss = notice.querySelector(".rw-docs-dismiss");
    if (dismiss) {
      dismiss.hidden = false;
      dismiss.addEventListener("click", () => {
        notice.hidden = true;
        try {
          sessionStorage.setItem(key, "true");
        } catch {
          // Dismiss this page even if the browser cannot remember the choice.
        }
        document.querySelector(".md-header a")?.focus();
      });
    }
  }

  function initialise() {
    const selector = document.querySelector(".md-version");
    const button = selector?.querySelector(".md-version__current");
    const list = selector?.querySelector(".md-version__list");
    if (!button || !list) return false;
    if (selector.hasAttribute("data-rw-click")) return true;

    selector.setAttribute("data-rw-click", "");
    list.id = "rw-documentation-versions";
    button.setAttribute("type", "button");
    button.setAttribute("aria-controls", list.id);

    function setOpen(open) {
      list.hidden = !open;
      button.setAttribute("aria-expanded", String(open));
    }

    setOpen(false);
    button.addEventListener("click", event => {
      event.stopPropagation();
      setOpen(list.hidden);
    });
    selector.addEventListener("keydown", event => {
      if (event.key === "Escape") {
        event.preventDefault();
        setOpen(false);
        button.focus();
      } else if (event.target === button && (event.key === "ArrowDown" || event.key === "ArrowUp")) {
        event.preventDefault();
        setOpen(true);
        const links = list.querySelectorAll("a");
        const link = event.key === "ArrowDown" ? links[0] : links[links.length - 1];
        link?.focus();
      }
    });
    selector.addEventListener("focusout", event => {
      if (!selector.contains(event.relatedTarget)) setOpen(false);
    });
    document.addEventListener("click", event => {
      if (!selector.contains(event.target)) setOpen(false);
    });
    return true;
  }

  if (!initialise()) {
    // Material creates the version selector after its inventory request finishes.
    const observer = new MutationObserver(() => {
      if (initialise()) observer.disconnect();
    });
    observer.observe(document.documentElement, {childList: true, subtree: true});
  }
})();
