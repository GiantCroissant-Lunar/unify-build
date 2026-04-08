"use strict";

const state = {
  allBuilds: [],
  filteredBuilds: [],
  charts: {},
  loadedFiles: new Set(),
};

const elements = {};

document.addEventListener("DOMContentLoaded", initialize);

function initialize() {
  elements.fileInput = document.getElementById("file-input");
  elements.dropZone = document.getElementById("drop-zone");
  elements.statsCards = document.getElementById("stats-cards");
  elements.chartsGrid = document.getElementById("charts-grid");
  elements.emptyState = document.getElementById("empty-state");
  elements.csvExport = document.getElementById("csv-export-btn");
  elements.clearData = document.getElementById("clear-data-btn");
  elements.filters = document.getElementById("filters");

  bindEvents();
  renderFilters();
  renderAll();
}

function bindEvents() {
  elements.fileInput.addEventListener("change", async (event) => {
    await handleFiles(event.target.files);
    event.target.value = "";
  });

  elements.dropZone.addEventListener("dragover", (event) => {
    event.preventDefault();
    elements.dropZone.classList.add("drag-over");
  });

  elements.dropZone.addEventListener("dragleave", () => {
    elements.dropZone.classList.remove("drag-over");
  });

  elements.dropZone.addEventListener("drop", async (event) => {
    event.preventDefault();
    elements.dropZone.classList.remove("drag-over");
    await handleFiles(event.dataTransfer.files);
  });

  elements.csvExport.addEventListener("click", exportCsv);
  elements.clearData.addEventListener("click", clearData);
}

function renderFilters() {
  elements.filters.innerHTML = `
    <div class="filter-group">
      <label for="filter-from">From</label>
      <input type="date" id="filter-from">
    </div>
    <div class="filter-group">
      <label for="filter-to">To</label>
      <input type="date" id="filter-to">
    </div>
    <div class="filter-group">
      <label for="filter-target">Build Target</label>
      <select id="filter-target"><option value="">All targets</option></select>
    </div>
    <div class="filter-group">
      <label for="filter-project">Project</label>
      <select id="filter-project"><option value="">All projects</option></select>
    </div>
    <button class="btn" id="apply-filters-btn" type="button">Apply</button>
    <button class="btn btn-outline" id="reset-filters-btn" type="button">Reset</button>
  `;

  document.getElementById("apply-filters-btn").addEventListener("click", applyFilters);
  document.getElementById("reset-filters-btn").addEventListener("click", resetFilters);
}

async function handleFiles(fileList) {
  const files = Array.from(fileList || []).filter((file) => file.name.toLowerCase().endsWith(".json"));
  if (!files.length) {
    return;
  }

  for (const file of files) {
    const fileKey = `${file.name}:${file.size}:${file.lastModified}`;
    if (state.loadedFiles.has(fileKey)) {
      continue;
    }

    try {
      const text = await file.text();
      const data = JSON.parse(text);
      const reports = Array.isArray(data) ? data : [data];
      let acceptedReports = 0;

      for (const report of reports) {
        const parsed = parseReport(report);
        if (parsed) {
          state.allBuilds.push(parsed);
          acceptedReports += 1;
        }
      }

      if (acceptedReports > 0) {
        state.loadedFiles.add(fileKey);
      }
    } catch (error) {
      console.warn(`Failed to parse ${file.name}`, error);
    }
  }

  state.allBuilds.sort((left, right) => new Date(left.timestamp) - new Date(right.timestamp));
  populateFilterDropdowns();
  applyFilters();
}

function clearData() {
  destroyCharts();
  state.allBuilds = [];
  state.filteredBuilds = [];
  state.loadedFiles.clear();
  resetFilters();
  populateFilterDropdowns();
  renderAll();
}

function parseReport(raw) {
  if (!raw || typeof raw !== "object") {
    return null;
  }

  return {
    timestamp: raw.timestamp || raw.Timestamp || new Date().toISOString(),
    totalDuration: parseDuration(raw.totalDuration || raw.TotalDuration || 0),
    targetDurations: parseDurationMap(raw.targetDurations || raw.TargetDurations || {}),
    projectDurations: parseDurationMap(raw.projectDurations || raw.ProjectDurations || {}),
    cacheHits: Number(raw.cacheHits ?? raw.CacheHits ?? 0),
    cacheMisses: Number(raw.cacheMisses ?? raw.CacheMisses ?? 0),
    cacheHitRate: Number(raw.cacheHitRate ?? raw.CacheHitRate ?? 0),
    success: Boolean(raw.success ?? raw.Success ?? false),
  };
}

function parseDurationMap(value) {
  const result = {};
  for (const [key, duration] of Object.entries(value)) {
    result[key] = parseDuration(duration);
  }
  return result;
}

function parseDuration(value) {
  if (typeof value === "number") {
    return value;
  }

  if (typeof value !== "string") {
    return 0;
  }

  const match = value.match(/^(?:(\d+)\.)?(\d{2}):(\d{2}):(\d{2})(?:\.(\d+))?$/);
  if (match) {
    const days = Number(match[1] || 0);
    const hours = Number(match[2]);
    const minutes = Number(match[3]);
    const seconds = Number(match[4]);
    const milliseconds = match[5] ? Number(match[5].padEnd(3, "0").slice(0, 3)) : 0;
    return days * 86400 + hours * 3600 + minutes * 60 + seconds + milliseconds / 1000;
  }

  const numeric = Number(value);
  return Number.isFinite(numeric) ? numeric : 0;
}

function populateFilterDropdowns() {
  const targetSelect = document.getElementById("filter-target");
  const projectSelect = document.getElementById("filter-project");
  const selectedTarget = targetSelect ? targetSelect.value : "";
  const selectedProject = projectSelect ? projectSelect.value : "";
  const targets = new Set();
  const projects = new Set();

  for (const build of state.allBuilds) {
    Object.keys(build.targetDurations).forEach((target) => targets.add(target));
    Object.keys(build.projectDurations).forEach((project) => projects.add(project));
  }

  if (targetSelect) {
    targetSelect.innerHTML = '<option value="">All targets</option>';
    Array.from(targets).sort().forEach((target) => {
      targetSelect.insertAdjacentHTML("beforeend", `<option value="${escapeHtml(target)}">${escapeHtml(target)}</option>`);
    });
    targetSelect.value = targets.has(selectedTarget) ? selectedTarget : "";
  }

  if (projectSelect) {
    projectSelect.innerHTML = '<option value="">All projects</option>';
    Array.from(projects).sort().forEach((project) => {
      projectSelect.insertAdjacentHTML("beforeend", `<option value="${escapeHtml(project)}">${escapeHtml(project)}</option>`);
    });
    projectSelect.value = projects.has(selectedProject) ? selectedProject : "";
  }
}

function applyFilters() {
  const fromValue = document.getElementById("filter-from")?.value || "";
  const toValue = document.getElementById("filter-to")?.value || "";
  const targetValue = document.getElementById("filter-target")?.value || "";
  const projectValue = document.getElementById("filter-project")?.value || "";

  state.filteredBuilds = state.allBuilds.filter((build) => {
    const timestamp = new Date(build.timestamp);
    if (fromValue && timestamp < new Date(fromValue)) {
      return false;
    }
    if (toValue && timestamp > new Date(`${toValue}T23:59:59`)) {
      return false;
    }
    if (targetValue && !(targetValue in build.targetDurations)) {
      return false;
    }
    if (projectValue && !(projectValue in build.projectDurations)) {
      return false;
    }
    return true;
  });

  renderAll();
}

function resetFilters() {
  const fromInput = document.getElementById("filter-from");
  const toInput = document.getElementById("filter-to");
  const targetSelect = document.getElementById("filter-target");
  const projectSelect = document.getElementById("filter-project");

  if (fromInput) {
    fromInput.value = "";
  }
  if (toInput) {
    toInput.value = "";
  }
  if (targetSelect) {
    targetSelect.value = "";
  }
  if (projectSelect) {
    projectSelect.value = "";
  }

  state.filteredBuilds = [...state.allBuilds];
  renderAll();
}

function renderAll() {
  elements.csvExport.disabled = state.filteredBuilds.length === 0;
  elements.clearData.disabled = state.allBuilds.length === 0;
  elements.dropZone.classList.toggle("has-data", state.allBuilds.length > 0);

  renderStats();
  renderCharts();
}

function renderStats() {
  const builds = state.filteredBuilds;
  const total = builds.length;
  const successes = builds.filter((build) => build.success).length;
  const failures = total - successes;
  const avgDuration = total ? builds.reduce((sum, build) => sum + build.totalDuration, 0) / total : 0;
  const totalHits = builds.reduce((sum, build) => sum + build.cacheHits, 0);
  const totalMisses = builds.reduce((sum, build) => sum + build.cacheMisses, 0);
  const cacheRate = totalHits + totalMisses > 0 ? totalHits / (totalHits + totalMisses) : 0;

  elements.statsCards.innerHTML = `
    <div class="stat-card">
      <div class="label">Loaded Builds</div>
      <div class="value">${total}</div>
      <div class="sub">${state.allBuilds.length} total in memory</div>
    </div>
    <div class="stat-card">
      <div class="label">Success Rate</div>
      <div class="value" style="color: ${getSuccessColor(total, successes)};">${total ? ((successes / total) * 100).toFixed(1) : "0.0"}%</div>
      <div class="sub">${successes} passed, ${failures} failed</div>
    </div>
    <div class="stat-card">
      <div class="label">Average Duration</div>
      <div class="value">${formatDuration(avgDuration)}</div>
      <div class="sub">Across filtered builds</div>
    </div>
    <div class="stat-card">
      <div class="label">Cache Hit Rate</div>
      <div class="value" style="color: ${getCacheColor(cacheRate)};">${(cacheRate * 100).toFixed(1)}%</div>
      <div class="sub">${totalHits} hits, ${totalMisses} misses</div>
    </div>
  `;
}

function renderCharts() {
  destroyCharts();

  if (!state.allBuilds.length) {
    showEmptyState("No metrics loaded yet", "Load one or more BuildMetrics JSON reports to see duration trends, cache rates, and slowest targets.");
    elements.chartsGrid.innerHTML = "";
    return;
  }

  if (!state.filteredBuilds.length) {
    showEmptyState("No builds match the current filters", "Reset the filters or load additional reports to populate the charts again.");
    elements.chartsGrid.innerHTML = "";
    return;
  }

  hideEmptyState();
  elements.chartsGrid.innerHTML = `
    <div class="chart-card"><h3>Build Duration Trend</h3><canvas id="chart-duration"></canvas></div>
    <div class="chart-card"><h3>Success vs Failure</h3><canvas id="chart-success"></canvas></div>
    <div class="chart-card"><h3>Cache Hit Rate Over Time</h3><canvas id="chart-cache"></canvas></div>
    <div class="chart-card"><h3>Slowest Targets</h3><canvas id="chart-targets"></canvas></div>
    <div class="chart-card wide"><h3>Project Compilation Time</h3><canvas id="chart-projects"></canvas></div>
  `;

  const labels = state.filteredBuilds.map((build) => formatDate(build.timestamp));
  const chartDefaults = createChartDefaults();

  state.charts.duration = new Chart(document.getElementById("chart-duration"), {
    type: "line",
    data: {
      labels,
      datasets: [{
        label: "Duration (seconds)",
        data: state.filteredBuilds.map((build) => build.totalDuration),
        borderColor: "#3b82f6",
        backgroundColor: "rgba(59, 130, 246, 0.14)",
        fill: true,
        tension: 0.3,
      }],
    },
    options: chartDefaults,
  });

  const successes = state.filteredBuilds.filter((build) => build.success).length;
  const failures = state.filteredBuilds.length - successes;
  state.charts.success = new Chart(document.getElementById("chart-success"), {
    type: "doughnut",
    data: {
      labels: ["Success", "Failure"],
      datasets: [{
        data: [successes, failures],
        backgroundColor: ["#22c55e", "#ef4444"],
      }],
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: {
          labels: { color: "#94a3b8" },
        },
      },
    },
  });

  state.charts.cache = new Chart(document.getElementById("chart-cache"), {
    type: "line",
    data: {
      labels,
      datasets: [{
        label: "Cache Hit Rate (%)",
        data: state.filteredBuilds.map((build) => Number((build.cacheHitRate * 100).toFixed(2))),
        borderColor: "#f59e0b",
        backgroundColor: "rgba(245, 158, 11, 0.14)",
        fill: true,
        tension: 0.3,
      }],
    },
    options: chartDefaults,
  });

  const targetAverages = computeAverageDurations(state.filteredBuilds, "targetDurations", 10);
  state.charts.targets = new Chart(document.getElementById("chart-targets"), {
    type: "bar",
    data: {
      labels: targetAverages.map((item) => item.name),
      datasets: [{
        label: "Average Duration (seconds)",
        data: targetAverages.map((item) => item.average),
        backgroundColor: "#8b5cf6",
      }],
    },
    options: {
      ...chartDefaults,
      indexAxis: "y",
    },
  });

  const projectAverages = computeAverageDurations(state.filteredBuilds, "projectDurations", 15);
  state.charts.projects = new Chart(document.getElementById("chart-projects"), {
    type: "bar",
    data: {
      labels: projectAverages.map((item) => item.name),
      datasets: [{
        label: "Average Duration (seconds)",
        data: projectAverages.map((item) => item.average),
        backgroundColor: "#06b6d4",
      }],
    },
    options: chartDefaults,
  });
}

function createChartDefaults() {
  return {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        labels: { color: "#94a3b8" },
      },
    },
    scales: {
      x: {
        ticks: { color: "#94a3b8" },
        grid: { color: "rgba(148, 163, 184, 0.12)" },
      },
      y: {
        beginAtZero: true,
        ticks: { color: "#94a3b8" },
        grid: { color: "rgba(148, 163, 184, 0.12)" },
      },
    },
  };
}

function computeAverageDurations(builds, key, limit) {
  const totals = new Map();
  const counts = new Map();

  for (const build of builds) {
    for (const [name, duration] of Object.entries(build[key])) {
      totals.set(name, (totals.get(name) || 0) + duration);
      counts.set(name, (counts.get(name) || 0) + 1);
    }
  }

  return Array.from(totals.entries())
    .map(([name, total]) => ({
      name,
      average: Number((total / counts.get(name)).toFixed(2)),
    }))
    .sort((left, right) => right.average - left.average)
    .slice(0, limit);
}

function exportCsv() {
  if (!state.filteredBuilds.length) {
    return;
  }

  const rows = [[
    "Timestamp",
    "TotalDuration_s",
    "Success",
    "CacheHits",
    "CacheMisses",
    "CacheHitRate_pct",
    "Targets",
    "Projects",
  ]];

  state.filteredBuilds.forEach((build) => {
    rows.push([
      build.timestamp,
      build.totalDuration.toFixed(3),
      build.success,
      build.cacheHits,
      build.cacheMisses,
      (build.cacheHitRate * 100).toFixed(2),
      Object.entries(build.targetDurations).map(([name, duration]) => `${name}=${duration.toFixed(3)}`).join(";"),
      Object.entries(build.projectDurations).map(([name, duration]) => `${name}=${duration.toFixed(3)}`).join(";"),
    ]);
  });

  const csv = rows.map((row) => row.map(toCsvCell).join(",")).join("\n");
  const blob = new Blob([csv], { type: "text/csv;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = `unifybuild-metrics-${new Date().toISOString().slice(0, 10)}.csv`;
  link.click();
  URL.revokeObjectURL(url);
}

function destroyCharts() {
  Object.values(state.charts).forEach((chart) => chart.destroy());
  state.charts = {};
}

function showEmptyState(title, message) {
  elements.emptyState.innerHTML = `<strong>${escapeHtml(title)}</strong><p>${escapeHtml(message)}</p>`;
  elements.emptyState.classList.add("visible");
}

function hideEmptyState() {
  elements.emptyState.classList.remove("visible");
  elements.emptyState.innerHTML = "";
}

function formatDuration(seconds) {
  if (seconds < 60) {
    return `${seconds.toFixed(1)}s`;
  }

  const minutes = Math.floor(seconds / 60);
  const remainder = Math.round(seconds % 60);
  return `${minutes}m ${remainder}s`;
}

function formatDate(value) {
  return new Date(value).toLocaleString(undefined, {
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

function getSuccessColor(total, successes) {
  if (!total) {
    return "var(--text)";
  }
  if (successes === total) {
    return "var(--success)";
  }
  if (successes === 0) {
    return "var(--danger)";
  }
  return "var(--warning)";
}

function getCacheColor(rate) {
  if (rate >= 0.8) {
    return "var(--success)";
  }
  if (rate >= 0.5) {
    return "var(--warning)";
  }
  return "var(--danger)";
}

function toCsvCell(value) {
  const text = String(value ?? "");
  if (/[",\n]/.test(text)) {
    return `"${text.replace(/"/g, '""')}"`;
  }
  return text;
}

function escapeHtml(value) {
  const element = document.createElement("span");
  element.textContent = value;
  return element.innerHTML;
}