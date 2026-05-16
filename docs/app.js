const statusEl = document.querySelector("#status");
const tbody = document.querySelector("#activity-body");
const todayTotal = document.querySelector("#today-total");
const todayLimit = document.querySelector("#today-limit");
const todayRemaining = document.querySelector("#today-remaining");
const lastUpdated = document.querySelector("#last-updated");
const dataRoots = ["data", "../data"];

function todayKey() {
  const now = new Date();
  const year = now.getFullYear();
  const month = String(now.getMonth() + 1).padStart(2, "0");
  const day = String(now.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function formatProcesses(processes = {}) {
  const entries = Object.entries(processes);
  if (entries.length === 0) return "-";
  return entries
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([name, value]) => `${name}: ${field(value, "time", "Time") ?? "00:00:00"}`)
    .join("; ");
}

function emptyDay(date) {
  return {
    date,
    totalTime: "00:00:00",
    limit: "00:00:00",
    remaining: "00:00:00",
    processes: {}
  };
}

async function loadDashboard() {
  try {
    const { root, index } = await loadIndex();
    const files = Array.isArray(index.files) ? index.files : [];

    const days = await Promise.all(files.map(async (file) => {
      const response = await fetch(`${root}/${file}`, { cache: "no-store" });
      if (!response.ok) return null;
      return response.json();
    }));

    const validDays = days.filter(Boolean).sort((a, b) => String(dayDate(a)).localeCompare(String(dayDate(b))));
    const lastSeven = validDays.slice(-7).reverse();
    const current = validDays.find((day) => dayDate(day) === todayKey()) ?? emptyDay(todayKey());

    todayTotal.textContent = field(current, "totalTime", "TotalTime") ?? "00:00:00";
    todayLimit.textContent = field(current, "limit", "Limit") ?? "00:00:00";
    todayRemaining.textContent = field(current, "remaining", "Remaining") ?? "00:00:00";
    const updated = field(current, "lastUpdatedLocal", "LastUpdatedLocal");
    lastUpdated.textContent = updated ? `Updated ${new Date(updated).toLocaleString()}` : "";

    tbody.innerHTML = "";
    for (const day of lastSeven) {
      const row = document.createElement("tr");
      row.innerHTML = `
        <td>${dayDate(day) ?? "-"}</td>
        <td>${field(day, "totalTime", "TotalTime") ?? "00:00:00"}</td>
        <td>${field(day, "limit", "Limit") ?? "00:00:00"}</td>
        <td>${field(day, "remaining", "Remaining") ?? "00:00:00"}</td>
        <td>${formatProcesses(field(day, "processes", "Processes"))}</td>
      `;
      tbody.appendChild(row);
    }

    if (lastSeven.length === 0) {
      const row = document.createElement("tr");
      row.innerHTML = '<td colspan="5" class="empty">No activity files yet.</td>';
      tbody.appendChild(row);
    }

    statusEl.textContent = "Live data";
  } catch (error) {
    statusEl.textContent = "Load failed";
    tbody.innerHTML = `<tr><td colspan="5" class="empty">${error.message}</td></tr>`;
  }
}

async function loadIndex() {
  const errors = [];

  for (const root of dataRoots) {
    try {
      const response = await fetch(`${root}/index.json`, { cache: "no-store" });
      if (response.ok) {
        return { root, index: await response.json() };
      }

      errors.push(`${root}/index.json returned ${response.status}`);
    } catch (error) {
      errors.push(`${root}/index.json failed: ${error.message}`);
    }
  }

  throw new Error(errors.join("; "));
}

function field(source, camelName, pascalName) {
  if (!source) return undefined;
  return source[camelName] ?? source[pascalName];
}

function dayDate(day) {
  return field(day, "date", "Date");
}

loadDashboard();
