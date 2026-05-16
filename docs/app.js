const statusEl = document.querySelector("#status");
const tbody = document.querySelector("#activity-body");
const todayTotal = document.querySelector("#today-total");
const todayLimit = document.querySelector("#today-limit");
const todayRemaining = document.querySelector("#today-remaining");
const lastUpdated = document.querySelector("#last-updated");

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
    .map(([name, value]) => `${name}: ${value.time ?? "00:00:00"}`)
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
    const indexResponse = await fetch("../data/index.json", { cache: "no-store" });
    if (!indexResponse.ok) throw new Error(`Could not load index.json (${indexResponse.status})`);
    const index = await indexResponse.json();
    const files = Array.isArray(index.files) ? index.files : [];

    const days = await Promise.all(files.map(async (file) => {
      const response = await fetch(`../data/${file}`, { cache: "no-store" });
      if (!response.ok) return null;
      return response.json();
    }));

    const validDays = days.filter(Boolean).sort((a, b) => String(a.date).localeCompare(String(b.date)));
    const lastSeven = validDays.slice(-7).reverse();
    const current = validDays.find((day) => day.date === todayKey()) ?? emptyDay(todayKey());

    todayTotal.textContent = current.totalTime ?? "00:00:00";
    todayLimit.textContent = current.limit ?? "00:00:00";
    todayRemaining.textContent = current.remaining ?? "00:00:00";
    lastUpdated.textContent = current.lastUpdatedLocal ? `Updated ${new Date(current.lastUpdatedLocal).toLocaleString()}` : "";

    tbody.innerHTML = "";
    for (const day of lastSeven) {
      const row = document.createElement("tr");
      row.innerHTML = `
        <td>${day.date ?? "-"}</td>
        <td>${day.totalTime ?? "00:00:00"}</td>
        <td>${day.limit ?? "00:00:00"}</td>
        <td>${day.remaining ?? "00:00:00"}</td>
        <td>${formatProcesses(day.processes)}</td>
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

loadDashboard();
