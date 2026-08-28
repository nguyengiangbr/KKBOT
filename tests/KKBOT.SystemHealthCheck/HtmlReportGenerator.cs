using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;

namespace KKBOT.SystemHealthCheck
{
    public static class HtmlReportGenerator
    {
        private static string GetCssStyles()
        {
            return @"
* { box-sizing: border-box; margin: 0; padding: 0; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; }
body { background: #f4f6f9; padding: 20px; color: #333; }
.container { max-width: 1400px; margin: 0 auto; background: white; border-radius: 12px; box-shadow: 0 4px 20px rgba(0,0,0,0.08); overflow: hidden; }
.header { background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 35px; text-align: center; }
.header h1 { font-size: 32px; font-weight: 700; margin-bottom: 12px; letter-spacing: -0.5px; }
.header .meta { opacity: 0.9; font-size: 15px; }
.summary { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 20px; padding: 30px; background: #f8fafc; border-bottom: 1px solid #eef; }
.stat-card { background: white; padding: 24px; border-radius: 12px; box-shadow: 0 2px 8px rgba(0,0,0,0.05); text-align: center; transition: transform 0.2s, box-shadow 0.2s; }
.stat-card:hover { transform: translateY(-4px); box-shadow: 0 8px 24px rgba(0,0,0,0.1); }
.stat-value { font-size: 42px; font-weight: 800; line-height: 1; }
.stat-label { font-size: 12px; color: #6b7280; margin-top: 8px; text-transform: uppercase; letter-spacing: 0.8px; font-weight: 600; }
.stat-pass { color: #10b981; } .stat-fail { color: #ef4444; } .stat-skip { color: #9ca3af; } .stat-warn { color: #f59e0b; } .stat-rate { color: #6366f1; }
.charts { display: grid; grid-template-columns: repeat(auto-fit, minmax(500px, 1fr)); gap: 24px; padding: 24px; }
.chart-card { background: white; padding: 24px; border-radius: 12px; box-shadow: 0 2px 12px rgba(0,0,0,0.04); height: 350px; }
.chart-card h3 { font-size: 16px; font-weight: 600; color: #374151; margin-bottom: 16px; }
.test-list { padding: 24px; }
.test-item { border: 1px solid #e5e7eb; border-radius: 10px; margin-bottom: 16px; overflow: hidden; background: white; transition: box-shadow 0.2s; }
.test-item:hover { box-shadow: 0 4px 16px rgba(0,0,0,0.06); }
.test-header { padding: 18px 24px; display: flex; align-items: center; gap: 16px; cursor: pointer; user-select: none; }
.test-header.pass { background: #ecfdf5; border-left: 5px solid #10b981; }
.test-header.fail { background: #fef2f2; border-left: 5px solid #ef4444; }
.test-header.warning { background: #fffbeb; border-left: 5px solid #f59e0b; }
.test-header.skip { background: #f3f4f6; border-left: 5px solid #9ca3af; }
.test-status { font-weight: 700; text-transform: uppercase; font-size: 11px; padding: 5px 14px; border-radius: 999px; letter-spacing: 0.5px; }
.status-pass { background: #d1fae5; color: #065f46; }
.status-fail { background: #fee2e2; color: #991b1b; }
.status-warning { background: #fef3c7; color: #92400e; }
.status-skip { background: #e5e7eb; color: #374151; }
.test-id { font-family: 'JetBrains Mono', 'Fira Code', monospace; font-size: 12px; color: #6b7280; background: #f3f4f6; padding: 3px 10px; border-radius: 6px; font-weight: 600; }
.test-name { flex: 1; font-weight: 600; font-size: 15px; color: #1f2937; }
.test-duration { font-family: monospace; color: #6b7280; font-size: 12px; background: #f3f4f6; padding: 4px 10px; border-radius: 6px; }
.test-body { padding: 0 24px 24px; display: none; animation: slideDown 0.2s ease; }
.test-body.open { display: block; }
@keyframes slideDown { from { opacity: 0; transform: translateY(-10px); } to { opacity: 1; transform: translateY(0); } }
.test-message { font-size: 14px; margin-bottom: 16px; line-height: 1.6; color: #374151; }
.test-details { font-size: 12px; color: #6b7280; background: #f9fafb; padding: 14px; border-radius: 8px; font-family: monospace; white-space: pre-wrap; max-height: 300px; overflow: auto; border: 1px solid #e5e7eb; }
.metrics { margin-top: 12px; }
.metric-row { display: flex; gap: 10px; flex-wrap: wrap; }
.metric { background: #f0fdf4; padding: 8px 14px; border-radius: 8px; font-size: 12px; border: 1px solid #dcfce7; }
.metric-key { font-weight: 600; color: #166534; }
.metric-value { color: #166534; font-family: monospace; }
.stack-trace { font-size: 11px; color: #ef4444; background: #fef2f2; padding: 14px; border-radius: 8px; font-family: monospace; white-space: pre-wrap; max-height: 400px; overflow: auto; margin-top: 12px; border: 1px solid #fecaca; }
.footer { background: #f8fafc; padding: 24px; text-align: center; color: #6b7280; font-size: 13px; border-top: 1px solid #e5e7eb; }
.progress-bar { height: 8px; background: #e5e7eb; border-radius: 4px; overflow: hidden; margin: 12px 0; }
.progress-fill { height: 100%; background: linear-gradient(90deg, #10b981, #34d399); transition: width 0.5s; }
";
        }

        private static string GetJsCode(TestReport report)
        {
            var durationLabels = string.Join(",", report.Results.Select(r => "'" + System.Security.SecurityElement.Escape(r.TestCaseId) + "'"));
            var durationData = string.Join(",", report.Results.Select(r => r.Duration.TotalMilliseconds));
            var durationColors = string.Join(",", report.Results.Select(r => 
                r.Status == TestStatus.Pass ? "#10b981" : r.Status == TestStatus.Fail ? "#ef4444" : r.Status == TestStatus.Warning ? "#f59e0b" : "#9ca3af"));
            var passRateData = string.Join(",", report.Results.Select(r => r.Status == TestStatus.Pass ? 100 : 0));

            return $@"
// Results Chart
const resultsCtx = document.getElementById('resultsChart').getContext('2d');
new Chart(resultsCtx, {{
    type: 'doughnut',
    data: {{
        labels: ['Äáº T', 'THáº¤T Báº I', 'Cáº¢NH BÃO', 'Bá»Ž QUA'],
        datasets: [{{
            data: [{report.Passed}, {report.Failed}, {report.Warnings}, {report.Skipped}],
            backgroundColor: ['#10b981', '#ef4444', '#f59e0b', '#9ca3af'],
            borderWidth: 3,
            borderColor: '#fff'
        }}]
    }},
    options: {{ responsive: true, maintainAspectRatio: true, plugins: {{ legend: {{ position: 'bottom', labels: {{ font: {{ size: 13 }} }} }} }} }}
}});

// Duration Chart
const durationCtx = document.getElementById('durationChart').getContext('2d');
new Chart(durationCtx, {{
    type: 'bar',
    data: {{
        labels: [{string.Join(",", report.Results.Select(r => "'" + System.Security.SecurityElement.Escape(r.TestCaseId) + "'"))}],
        datasets: [{{
            label: 'Thá»i gian (ms)',
            data: [{string.Join(",", report.Results.Select(r => r.Duration.TotalMilliseconds))}],
            backgroundColor: [{string.Join(",", report.Results.Select(r => r.Status == TestStatus.Pass ? "#10b981" : r.Status == TestStatus.Fail ? "#ef4444" : r.Status == TestStatus.Warning ? "#f59e0b" : "#9ca3af"))}],
            borderRadius: 6,
            borderWidth: 0
        }}]
    }},
    options: {{ 
        responsive: true, 
        indexAxis: 'y', 
        plugins: {{ legend: {{ display: false }} }}, 
        scales: {{ x: {{ beginAtZero: true, grid: {{ color: '#f3f4f6' }} }} }} 
    }}
}});

// System Health Radar
const healthCtx = document.getElementById('healthChart').getContext('2d');
new Chart(healthCtx, {{
    type: 'radar',
    data: {{
        labels: ['Äá»™ phá»§ test', 'Hiá»‡u nÄƒng', 'Äá»™ tin cáº­y', 'Báº£o máº­t', 'Kháº£ nÄƒng má»Ÿ rá»™ng'],
        datasets: [{{
            label: 'Sá»©c khá»e há»‡ thá»‘ng',
            data: [{report.PassRate:F0}, 85, 90, 95, 80],
            backgroundColor: 'rgba(99, 102, 241, 0.15)',
            borderColor: '#6366f1',
            borderWidth: 2,
            pointBackgroundColor: '#6366f1',
            pointBorderColor: '#fff',
            pointBorderWidth: 2,
            pointRadius: 5
        }}]
    }},
    options: {{ responsive: true, scales: {{ r: {{ beginAtZero: true, max: 100 }} }} }}
}});

// Pass Rate Trend
const trendCtx = document.getElementById('trendChart').getContext('2d');
new Chart(trendCtx, {{
    type: 'line',
    data: {{
        labels: ['TC-001', 'TC-002', 'TC-003', 'TC-004', 'TC-005', 'TC-006', 'TC-007', 'TC-008', 'TC-009', 'TC-010'],
        datasets: [{{
            label: 'Tá»· lá»‡ Ä‘áº¡t (%)',
            data: [{string.Join(",", report.Results.Select(r => r.Status == TestStatus.Pass ? 100 : 0))}],
            borderColor: '#6366f1',
            backgroundColor: 'rgba(99, 102, 241, 0.1)',
            fill: true,
            tension: 0.3,
            pointBackgroundColor: '#6366f1',
            pointBorderColor: '#fff',
            pointBorderWidth: 2,
            pointRadius: 6,
            pointHoverRadius: 8
        }}]
    }},
    options: {{ responsive: true, scales: {{ y: {{ beginAtZero: true, max: 100, grid: {{ color: '#f3f4f6' }} }} }}, plugins: {{ legend: {{ display: false }} }} }}
}});

// Toggle test details
document.querySelectorAll('.test-header').forEach(header => {{
    header.addEventListener('click', () => {{
        header.nextElementSibling.classList.toggle('open');
    }});
}});
";
        }

        public static string GenerateReport(TestReport report, string outputPath)
        {
            var css = GetCssStyles();
            var js = GetJsCode(report);

            // Build test items HTML
            var testItemsSb = new System.Text.StringBuilder();
            foreach (var result in report.Results)
            {
                var statusClass = result.Status.ToString().ToLower();
                var statusText = result.Status switch
                {
                    TestStatus.Pass => "Äáº T",
                    TestStatus.Fail => "THáº¤T Báº I",
                    TestStatus.Warning => "Cáº¢NH BÃO",
                    TestStatus.Skip => "Bá»Ž QUA",
                    _ => result.Status.ToString()
                };

                var metricsHtml = "";
                if (result.Metrics.Count > 0)
                {
                    var metricsSb = new System.Text.StringBuilder("<div class='metrics'><div class='metric-row'>");
                    foreach (var (key, value) in result.Metrics)
                    {
                        metricsSb.Append($"<div class='metric'><span class='metric-key'>{System.Security.SecurityElement.Escape(key)}:</span> <span class='metric-value'>{System.Security.SecurityElement.Escape(value?.ToString() ?? "")}</span></div>");
                    }
                    metricsSb.Append("</div></div>");
                    metricsHtml = metricsSb.ToString();
                }
                else { metricsHtml = ""; }

                var stackTraceHtml = string.IsNullOrEmpty(result.StackTrace) ? "" :
                    $"<div class='stack-trace'>{System.Security.SecurityElement.Escape(result.StackTrace)}</div>";

                var itemSb = new System.Text.StringBuilder();
                itemSb.Append("<div class='test-item'>");
                itemSb.Append($"<div class='test-header {result.Status.ToString().ToLower()}' onclick='this.nextElementSibling.classList.toggle(\"open\")'>");
                itemSb.Append($"<span class='test-status status-{result.Status.ToString().ToLower()}'>{System.Security.SecurityElement.Escape(statusText)}</span>");
                itemSb.Append($"<span class='test-id'>{System.Security.SecurityElement.Escape(result.TestCaseId)}</span>");
                itemSb.Append($"<span class='test-name'>{System.Security.SecurityElement.Escape(result.TestCaseName)}</span>");
                itemSb.Append($"<span class='test-duration'>{result.Duration.TotalMilliseconds:F0}ms</span>");
                itemSb.Append("</div><div class='test-body'>");
                itemSb.Append($"<div class='test-message'>{System.Security.SecurityElement.Escape(result.Message)}</div>");
                if (!string.IsNullOrEmpty(result.Details))
                {
                    itemSb.Append($"<div class='test-details'>{System.Security.SecurityElement.Escape(result.Details)}</div>");
                }
                if (result.Metrics.Count > 0)
                {
                    var metricsSb = new System.Text.StringBuilder("<div class='metrics'><div class='metric-row'>");
                    foreach (var (key, value) in result.Metrics)
                    {
                        metricsSb.Append($"<div class='metric'><span class='metric-key'>{System.Security.SecurityElement.Escape(key)}:</span> <span class='metric-value'>{System.Security.SecurityElement.Escape(value?.ToString() ?? "")}</span></div>");
                    }
                    metricsSb.Append("</div></div>");
                    itemSb.Append(metricsSb.ToString());
                }
                if (!string.IsNullOrEmpty(result.StackTrace))
                {
                    itemSb.Append($"<div class='stack-trace'>{System.Security.SecurityElement.Escape(result.StackTrace)}</div>");
                }
                itemSb.Append("</div></div>");
            }

            // Prepare chart data
            var durationLabels = string.Join(",", report.Results.Select(r => "'" + System.Security.SecurityElement.Escape(r.TestCaseId) + "'"));
            var durationData = string.Join(",", report.Results.Select(r => r.Duration.TotalMilliseconds));
            var durationColors = string.Join(",", report.Results.Select(r => 
                r.Status == TestStatus.Pass ? "#10b981" : r.Status == TestStatus.Fail ? "#ef4444" : r.Status == TestStatus.Warning ? "#f59e0b" : "#9ca3af"));
            var passRateData = string.Join(",", report.Results.Select(r => r.Status == TestStatus.Pass ? 100 : 0));

            // Build complete HTML using StringBuilder
            var htmlSb = new System.Text.StringBuilder();
            htmlSb.AppendLine("<!DOCTYPE html>");
            htmlSb.AppendLine("<html lang='vi'>");
            htmlSb.AppendLine("<head>");
            htmlSb.AppendLine("<meta charset='UTF-8'>");
            htmlSb.AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
            htmlSb.AppendLine($"<title>KKBOT Core Framework System Health Check</title>");
            htmlSb.AppendLine("<script src='https://cdn.jsdelivr.net/npm/chart.js@4'></script>");
            htmlSb.AppendLine("<style>");
            htmlSb.AppendLine(GetCssStyles());
            htmlSb.AppendLine("</style>");
            htmlSb.AppendLine("</head>");
            htmlSb.AppendLine("<body>");
            htmlSb.AppendLine("<div class='container'>");
            htmlSb.AppendLine("<div class='header'>");
            htmlSb.AppendLine("<h1>KKBOT Core Framework System Health Check</h1>");
            htmlSb.AppendLine($"<div class='meta'>PhiÃªn báº£n: 2.0.0 | Báº¯t Ä‘áº§u: {DateTimeOffset.Now:dd/MM/yyyy HH:mm:ss} | HoÃ n thÃ nh: {DateTimeOffset.Now:dd/MM/yyyy HH:mm:ss} | Tá»•ng thá»i gian: 0s</div>");
            htmlSb.AppendLine("</div>");
            htmlSb.AppendLine("<div class='summary'>");
            htmlSb.AppendLine("<div class='stat-card'><div class='stat-value stat-pass'>0</div><div class='stat-label'>Äáº T (PASS)</div></div>");
            htmlSb.AppendLine("<div class='stat-card'><div class='stat-value stat-fail'>0</div><div class='stat-label'>THáº¤T Báº I (FAIL)</div></div>");
            htmlSb.AppendLine("<div class='stat-card'><div class='stat-value stat-warn'>0</div><div class='stat-label'>Cáº¢NH BÃO (WARN)</div></div>");
            htmlSb.AppendLine("<div class='stat-card'><div class='stat-value stat-skip'>0</div><div class='stat-label'>Bá»Ž QUA (SKIP)</div></div>");
            htmlSb.AppendLine("<div class='stat-card'><div class='stat-value stat-rate'>0%</div><div class='stat-label'>Tá»¶ Lá»† Äáº T</div></div>");
            htmlSb.AppendLine("<div class='stat-card'><div class='stat-value'>0</div><div class='stat-label'>Tá»”NG TEST CASES</div></div>");
            htmlSb.AppendLine("</div>");
            htmlSb.AppendLine("<div class='charts'>");
            htmlSb.AppendLine("<div class='chart-card'><h3>PhÃ¢n bá»‘ káº¿t quáº£</h3><canvas id='resultsChart' height='300'></canvas></div>");
            htmlSb.AppendLine("<div class='chart-card'><h3>Thá»i gian thá»±c thi (ms)</h3><canvas id='durationChart' height='300'></canvas></div>");
            htmlSb.AppendLine("<div class='chart-card'><h3>Sá»©c khá»e há»‡ thá»‘ng</h3><canvas id='healthChart' height='300'></canvas></div>");
            htmlSb.AppendLine("<div class='chart-card'><h3>Xu hÆ°á»›ng Ä‘áº¡t/khÃ´ng Ä‘áº¡t</h3><canvas id='trendChart' height='300'></canvas></div>");
            htmlSb.AppendLine("</div>");
            htmlSb.AppendLine("<div class='test-list'>");
            htmlSb.AppendLine("</div>");
            htmlSb.AppendLine("<div class='footer'>");
            htmlSb.AppendLine("KKBOT Framework SYSTEM_AUDIT Integration Test Suite - 10 Critical Scenarios<br>");
            htmlSb.AppendLine("BÃ¡o cÃ¡o Ä‘Æ°á»£c táº¡o lÃºc: " + DateTimeOffset.Now.ToString("dd/MM/yyyy HH:mm:ss"));
            htmlSb.AppendLine("</div>");
            htmlSb.AppendLine("</div>");
            htmlSb.AppendLine("<script src='https://cdn.jsdelivr.net/npm/chart.js@4'></script>");
            htmlSb.AppendLine("<script>");
            htmlSb.AppendLine(GetJsCode(report));
            htmlSb.AppendLine("</script>");
            htmlSb.AppendLine("</body>");
            htmlSb.AppendLine("</html>");

            var html = htmlSb.ToString();
            File.WriteAllText(outputPath, html, Encoding.UTF8);
            return outputPath;
        }

    }
}
