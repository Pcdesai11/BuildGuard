(function () {
  var banner = document.getElementById("banner");
  var status = document.getElementById("status");
  var empty = document.getElementById("empty");
  var summary = document.getElementById("summary");
  var charts = document.getElementById("charts");
  var tableWrap = document.getElementById("table-wrap");
  var usingSample = false;

  document.getElementById("preview").addEventListener("click", function () {
    usingSample = true;
    render(sampleRuns());
  });

  fetch("history.json", { cache: "no-store" })
    .then(function (response) {
      if (!response.ok) {
        throw new Error("history.json returned " + response.status);
      }
      return response.json();
    })
    .then(function (payload) {
      if (usingSample) {
        return;
      }
      var runs = payload && Array.isArray(payload.runs) ? payload.runs : [];
      render(runs);
    })
    .catch(function (error) {
      status.textContent = "Could not read history.json. " + error.message;
    });

  function render(runs) {
    status.hidden = true;
    banner.hidden = !usingSample;
    banner.textContent = usingSample
      ? "Sample preview. These numbers are invented in the browser so you can see the layout. They are not CI results and they are not saved."
      : "";

    if (!runs.length) {
      empty.hidden = false;
      summary.hidden = true;
      charts.hidden = true;
      tableWrap.hidden = true;
      return;
    }

    empty.hidden = true;
    summary.hidden = false;
    charts.hidden = false;
    tableWrap.hidden = false;

    var latest = runs[runs.length - 1];
    var rate = passRate(latest);
    document.getElementById("pass-rate").textContent = rate == null ? "—" : Math.round(rate * 100) + "%";
    document.getElementById("pass-rate").className = "figure " + rateClass(rate);
    document.getElementById("pass-detail").textContent = testDetail(latest);
    document.getElementById("duration").textContent = formatMinutes(latest.pipelineDurationSeconds);
    var warnings = latest.validator && latest.validator.ran ? latest.validator.warnings : null;
    document.getElementById("warnings").textContent = warnings == null ? "—" : String(warnings);
    document.getElementById("warnings").className = "figure " + (warnings > 0 ? "warn" : "");
    document.getElementById("warning-detail").textContent = validatorDetail(latest);
    document.getElementById("run-count").textContent = String(runs.length);

    document.getElementById("pass-chart").innerHTML = lineChart(runs, function (run) {
      var value = passRate(run);
      return value == null ? null : value * 100;
    }, "%");
    document.getElementById("duration-chart").innerHTML = lineChart(runs, function (run) {
      return run.pipelineDurationSeconds == null ? null : run.pipelineDurationSeconds / 60;
    }, " min");

    var rows = document.getElementById("rows");
    rows.textContent = "";
    for (var i = runs.length - 1; i >= 0; i--) {
      rows.appendChild(rowFor(runs[i]));
    }
  }

  function rowFor(run) {
    var tr = document.createElement("tr");
    var rate = passRate(run);
    var cells = [
      run.recordedUtc || "—",
      commitLink(run),
      testDetail(run),
      rate == null ? "—" : Math.round(rate * 100) + "%",
      formatMinutes(run.pipelineDurationSeconds),
      run.playerBuildDurationSeconds == null ? "—" : run.playerBuildDurationSeconds + "s",
      validatorDetail(run),
      run.buildResult || "—"
    ];
    cells.forEach(function (value, index) {
      var td = document.createElement("td");
      if (index === 1 && value instanceof Node) {
        td.appendChild(value);
      } else {
        td.textContent = value;
      }
      if (index === 3 && rate != null) {
        td.className = rateClass(rate);
      }
      if (index === 7 && run.buildResult && run.buildResult !== "Succeeded") {
        td.className = run.buildResult === "not-run" ? "warn" : "fail";
      }
      tr.appendChild(td);
    });
    return tr;
  }

  function commitLink(run) {
    var shortSha = (run.commitSha || "").slice(0, 7) || "—";
    if (!run.runUrl) {
      return document.createTextNode(shortSha);
    }
    var link = document.createElement("a");
    link.href = run.runUrl;
    link.textContent = shortSha;
    return link;
  }

  function passRate(run) {
    if (!run.tests) {
      return null;
    }
    var decided = (run.tests.passed || 0) + (run.tests.failed || 0);
    if (decided === 0) {
      return null;
    }
    return run.tests.passed / decided;
  }

  function rateClass(rate) {
    if (rate == null) {
      return "";
    }
    return rate === 1 ? "pass" : "fail";
  }

  function testDetail(run) {
    if (!run.tests) {
      return "—";
    }
    return (run.tests.passed || 0) + " passed, " + (run.tests.failed || 0) + " failed, " + (run.tests.skipped || 0) + " skipped";
  }

  function validatorDetail(run) {
    if (!run.validator || !run.validator.ran) {
      return "not run";
    }
    return (run.validator.errors || 0) + " errors, " + (run.validator.warnings || 0) + " warnings";
  }

  function formatMinutes(seconds) {
    if (seconds == null) {
      return "—";
    }
    return (seconds / 60).toFixed(1) + " min";
  }

  function lineChart(runs, valueOf, suffix) {
    var width = 520;
    var height = 180;
    var pad = 28;
    var values = runs.map(valueOf);
    var numeric = values.filter(function (value) { return value != null; });
    if (!numeric.length) {
      return "<p class=\"detail\">No numeric points yet.</p>";
    }
    var min = Math.min.apply(null, numeric);
    var max = Math.max.apply(null, numeric);
    if (min === max) {
      min = min - 1;
      max = max + 1;
    }
    var coords = [];
    values.forEach(function (value, index) {
      if (value == null) {
        return;
      }
      var x = pad + (runs.length === 1 ? (width - pad * 2) / 2 : (index / (runs.length - 1)) * (width - pad * 2));
      var y = pad + (1 - (value - min) / (max - min)) * (height - pad * 2);
      coords.push(x.toFixed(1) + "," + y.toFixed(1));
    });
    var last = numeric[numeric.length - 1];
    return "<svg viewBox=\"0 0 " + width + " " + height + "\" role=\"img\">"
      + "<line x1=\"" + pad + "\" y1=\"" + (height - pad) + "\" x2=\"" + (width - 8) + "\" y2=\"" + (height - pad) + "\" stroke=\"#2c3644\"/>"
      + "<polyline fill=\"none\" stroke=\"#8eb7e8\" stroke-width=\"2\" points=\"" + coords.join(" ") + "\"/>"
      + "<text x=\"" + pad + "\" y=\"16\">" + max.toFixed(1) + suffix + "</text>"
      + "<text x=\"" + pad + "\" y=\"" + (height - 8) + "\">" + min.toFixed(1) + suffix + "</text>"
      + "<text x=\"" + (width - 90) + "\" y=\"16\">latest " + last.toFixed(1) + suffix + "</text>"
      + "</svg>";
  }

  function sampleRuns() {
    var shape = [0.8, 1, 0.9, 1, 1, 0.7, 1];
    var minutes = [18, 16, 15, 9, 8.5, 11, 8];
    var runs = [];
    for (var i = 0; i < shape.length; i++) {
      var passed = Math.round(shape[i] * 20);
      runs.push({
        commitSha: "sample" + i,
        recordedUtc: "sample",
        runUrl: "",
        pipelineDurationSeconds: Math.round(minutes[i] * 60),
        playerBuildDurationSeconds: 30 + i,
        tests: { total: 20, passed: passed, failed: 20 - passed, skipped: 0 },
        validator: { ran: true, errors: 0, warnings: i === 5 ? 3 : 0 },
        buildResult: shape[i] === 1 ? "Succeeded" : "Failed"
      });
    }
    return runs;
  }
})();
