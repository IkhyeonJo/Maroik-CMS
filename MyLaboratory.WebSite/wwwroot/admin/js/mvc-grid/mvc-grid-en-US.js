/*!
 * Mvc.Grid 6.2.4
 *
 * Copyright © NonFactors
 *
 * Licensed under the terms of the MIT License
 * https://www.opensource.org/licenses/mit-license.php
 */
class MvcGrid {
    constructor(container, options = {}) {
        const grid = this;
        const element = grid.findGrid(container);

        if (element.dataset.id) {
            return MvcGrid.instances[parseInt(element.dataset.id)].set(options);
        }

        grid.columns = [];
        grid.element = element;
        grid.loadingDelay = 300;
        grid.loadingTimerId = 0;
        grid.name = element.dataset.name;
        grid.controller = new AbortController();
        grid.isAjax = Boolean(element.dataset.url);
        grid.prefix = grid.name ? `${grid.name}-` : "";
        grid.filterMode = (element.dataset.filterMode || "").toLowerCase();
        element.dataset.id = options.id || MvcGrid.instances.length.toString();
        grid.url = element.dataset.url ? new URL(element.dataset.url, location.href) : new URL(location.href);
        grid.url = options.url ? new URL(options.url.toString(), location.href) : grid.url;
        grid.url = options.query ? new URL(`?${options.query}`, grid.url.href) : grid.url;
        grid.sort = grid.buildSort();
        grid.filters = {
            default: MvcGridFilter,
            date: MvcGridDateFilter,
            guid: MvcGridGuidFilter,
            text: MvcGridTextFilter,
            number: MvcGridNumberFilter
        };

        const headers = element.querySelector(".mvc-grid-headers");
        const rowFilters = element.querySelectorAll(".mvc-grid-row-filters th");

        if (headers) {
            for (const [i, header] of headers.querySelectorAll("th").entries()) {
                grid.columns.push(new MvcGridColumn(grid, header, rowFilters[i]));
            }
        }

        const pager = element.querySelector(".mvc-grid-pager");

        if (pager) {
            grid.pager = new MvcGridPager(grid, pager);
        }

        grid.set(options);
        grid.cleanUp();
        grid.bind();

        if (options.id) {
            MvcGrid.instances[parseInt(options.id)] = grid;
        } else {
            MvcGrid.instances.push(grid);
        }

        if (!element.children.length) {
            grid.reload();
        }
    }

    set(options) {
        const grid = this;

        grid.loadingDelay = typeof options.loadingDelay == "number" ? options.loadingDelay : grid.loadingDelay;
        grid.url = options.url ? new URL(options.url.toString(), location.href) : grid.url;
        grid.url = options.query ? new URL(`?${options.query}`, grid.url.href) : grid.url;
        grid.isAjax = typeof options.isAjax == "boolean" ? options.isAjax : grid.isAjax;
        grid.filters = Object.assign(grid.filters, options.filters);

        for (const column of grid.columns) {
            if (column.filter && grid.filters[column.filter.name]) {
                column.filter.instance = new grid.filters[column.filter.name](column);
                column.filter.instance.init();
            }
        }

        return grid;
    }
    showConfiguration(anchor) {
        MvcGridPopup.showConfiguration(this, anchor);
    }
    getConfiguration() {
        return {
            name: this.name,
            columns: this.columns.map(column => ({ name: column.name, hidden: column.isHidden }))
        };
    }
    configure(configuration) {
        configuration.columns.forEach((column, index) => {
            const rows = this.element.querySelectorAll("tr");
            const i = this.columns.findIndex(col => col.name.toLowerCase() == column.name.toLowerCase());

            if (i >= 0) {
                this.columns[i].isHidden = column.hidden == true;

                for (const tr of rows) {
                    if (column.hidden) {
                        tr.children[i].classList.add("mvc-grid-hidden");
                    } else {
                        tr.children[i].classList.remove("mvc-grid-hidden");
                    }

                    if (i != index) {
                        tr.insertBefore(tr.children[i], tr.children[index]);
                    }
                }

                this.columns.splice(i - (index < i ? 1 : 0), 0, this.columns.splice(index, 1)[0]);
            }
        });
    }

    reload() {
        const grid = this;

        grid.element.dispatchEvent(new CustomEvent("reloadstart", {
            detail: { grid },
            bubbles: true
        }));

        if (grid.isAjax) {
            const url = new URL(grid.url.href);

            grid.controller.abort();
            MvcGridPopup.lastActiveElement = null;
            grid.controller = new AbortController();
            url.searchParams.set("_", String(Date.now()));

            if (grid.loadingDelay != null) {
                if (grid.loader && grid.loader.parentElement) {
                    clearTimeout(grid.loadingTimerId);
                } else {
                    const loader = document.createElement("template");

                    loader.innerHTML = `<div class="mvc-grid-loader"><div><div></div><div></div><div></div></div></div>`;
                    grid.loader = loader.content.firstElementChild;

                    grid.element.appendChild(grid.loader);
                }

                grid.loadingTimerId = setTimeout(() => {
                    grid.loader.classList.add("mvc-grid-loading");
                }, grid.loadingDelay);
            }

            MvcGridPopup.hide();

            fetch(url.href, {
                signal: grid.controller.signal,
                headers: { "X-Requested-With": "XMLHttpRequest" }
            }).then(response => {
                if (!response.ok) {
                    throw new Error(`Invalid response status: ${response.status}`);
                }

                return response.text();
            }).then(response => {
                const parent = grid.element.parentElement;
                const template = document.createElement("template");
                const i = Array.from(parent.children).indexOf(grid.element);

                template.innerHTML = response.trim();

                if (template.content.firstElementChild.classList.contains("mvc-grid")) {
                    grid.element.outerHTML = response;
                } else {
                    throw new Error("Grid partial should only include grid declaration.");
                }

                const newGrid = new MvcGrid(parent.children[i], {
                    loadingDelay: grid.loadingDelay,
                    id: grid.element.dataset.id,
                    filters: grid.filters,
                    isAjax: grid.isAjax,
                    url: grid.url
                });

                newGrid.element.dispatchEvent(new CustomEvent("reloadend", {
                    detail: { grid: newGrid },
                    bubbles: true
                }));
            }).catch(reason => {
                if (reason.name == "AbortError") {
                    return Promise.resolve();
                }

                if (grid.loader && grid.loader.parentElement) {
                    grid.loader.parentElement.removeChild(grid.loader);
                }

                const cancelled = !grid.element.dispatchEvent(new CustomEvent("reloadfail", {
                    detail: { grid, reason },
                    cancelable: true,
                    bubbles: true
                }));

                return cancelled ? Promise.resolve() : Promise.reject(reason);
            });
        } else {
            location.href = grid.url.href;
        }
    }

    buildSort() {
        const map = new Map();
        const definitions = /(^|,)(.*?) (asc|desc)(?=$|,)/g;
        const sort = this.url.searchParams.get(`${this.prefix}sort`) || "";

        let match = definitions.exec(sort);

        while (match) {
            map.set(match[2], match[3]);

            match = definitions.exec(sort);
        }

        return map;
    }
    findGrid(element) {
        const grid = element.closest(".mvc-grid");

        if (!grid) {
            throw new Error("Grid can only be created from within mvc-grid structure.");
        }

        return grid;
    }
    cleanUp() {
        delete this.element.dataset.filterMode;
        delete this.element.dataset.url;
    }
    bind() {
        const grid = this;

        for (const row of grid.element.querySelectorAll("tbody > tr")) {
            if (!row.classList.contains("mvc-grid-empty-row")) {
                row.addEventListener("click", function (e) {
                    const data = {};

                    for (const [i, column] of grid.columns.entries()) {
                        if (row.cells.length <= i) {
                            return;
                        }

                        data[column.name] = row.cells[i].innerText;
                    }

                    this.dispatchEvent(new CustomEvent("rowclick", {
                        detail: { grid: grid, data: data, originalEvent: e },
                        bubbles: true
                    }));
                });
            }
        }
    }
}