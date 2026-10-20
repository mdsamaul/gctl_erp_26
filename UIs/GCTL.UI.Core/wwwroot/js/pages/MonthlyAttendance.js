// ═══════════════════════════════════════════════════════════════════
// File: wwwroot/js/pages/MonthlyAttendance.js
// ═══════════════════════════════════════════════════════════════════
(function ($) {
    $.monthlyAttendance = function (options) {
        var settings = $.extend({ baseUrl: "/MonthlyAttendance" }, options);

        var base = (settings.baseUrl || "/MonthlyAttendance").replace(/\/Index\/?$/i, '').replace(/\/+$/, '');
        if (!base) base = "/MonthlyAttendance";

        var GetSummaryUrl    = base + "/GetSummary";
        var DownloadExcelUrl = base + "/DownloadExcel";
        var DownloadCsvUrl   = base + "/DownloadCsv";
        var PreviewPdfUrl    = base + "/PreviewPdf";
        var DownloadPdfUrl   = base + "/DownloadPdf";

        // ── Loading Overlay ───────────────────────────────────────────
        (function setupOverlay() {
            if ($("#monthlyAttendanceLoadingOverlay").length === 0) {
                $("body").append(`
                    <div id="monthlyAttendanceLoadingOverlay" style="
                        display:none;position:fixed;top:0;left:0;
                        width:100%;height:100%;background:rgba(15,23,42,0.65);
                        backdrop-filter:blur(4px);-webkit-backdrop-filter:blur(4px);
                        z-index:999999;justify-content:center;align-items:center;">
                        <div style="background:#fff;padding:24px 36px;border-radius:10px;
                                    box-shadow:0 10px 25px rgba(0,0,0,0.25);text-align:center;min-width:220px;">
                            <div class="spinner-border text-success" style="width:3rem;height:3rem;" role="status">
                                <span class="sr-only">Loading...</span>
                            </div>
                            <h6 id="monthlyAttLoadingMsg" style="margin-top:14px;margin-bottom:0;color:#1e293b;font-weight:600;">Processing Monthly Attendance Report...</h6>
                            <small style="color:#64748b;">Please wait a moment</small>
                        </div>
                    </div>`);
            }
        })();

        function showLoading(msg) {
            if (msg) $("#monthlyAttLoadingMsg").text(msg);
            else $("#monthlyAttLoadingMsg").text("Processing Monthly Attendance Report...");
            $("#monthlyAttendanceLoadingOverlay").css("display", "flex");
        }

        function hideLoading() {
            $("#monthlyAttendanceLoadingOverlay").hide();
        }

        // ── Multi-Select Helper ──────────────────────────────────────
        function getMultiVal(sel) {
            var v = $(sel).val();
            if (!v) return null;
            var arr = Array.isArray(v) ? v : [v];
            arr = arr.filter(function (x) { return x && x !== ""; });
            return arr.length > 0 ? arr : null;
        }

        // ── Filter Builder ───────────────────────────────────────────
        function getFilter() {
            var compArr = getMultiVal("#companySelect");
            var compCode = compArr && compArr.length > 0 ? compArr[0] : null;

            var branchArr = getMultiVal("#branchSelect");
            var branchCode = branchArr && branchArr.length > 0 ? branchArr[0] : null;

            var durationType = $('input[name="durationRadio"]:checked').val() || "ByDate";

            return {
                CompanyCode: compCode,
                CompanyCodes: compArr,
                BranchCode: branchCode,
                BranchCodes: branchArr,
                DepartmentCodes: getMultiVal("#departmentSelect"),
                DesignationCodes: getMultiVal("#designationSelect"),
                EmploymentNatureCodes: getMultiVal("#employmentNatureSelect"),
                EmployeeIds: getMultiVal("#employeeSelect"),
                DurationType: durationType,
                FromDate: durationType === "ByDate" ? ($("#fromDateSelect").val() || null) : null,
                ToDate: durationType === "ByDate" ? ($("#toDateSelect").val() || null) : null,
                Month: durationType === "ByMonth" ? (parseInt($("#monthSelect").val(), 10) || null) : null,
                Year: durationType === "ByMonth" ? (parseInt($("#yearSelect").val(), 10) || null) : null,
                ExportFormat: $("#reportFormatSelect").val()
            };
        }

        // ── Date Pickers Setup ───────────────────────────────────────
        var now = new Date();
        var defaultFromDate = new Date(now.getFullYear(), now.getMonth(), 1); // 1st day of month
        var defaultToDate = new Date(now.getFullYear(), now.getMonth() + 1, 0); // Last day of month (e.g. 31st)

        var fpFrom = flatpickr("#fromDateSelect", {
            dateFormat: "d/m/Y",
            defaultDate: defaultFromDate,
            allowInput: true
        });

        var fpTo = flatpickr("#toDateSelect", {
            dateFormat: "d/m/Y",
            defaultDate: defaultToDate,
            allowInput: true
        });

        // Set default month in dropdown
        var currentMonthNum = (now.getMonth() + 1).toString();
        if (!$("#monthSelect").val()) {
            $("#monthSelect").val(currentMonthNum);
        }

        // ── Duration Radio Toggle (By Date vs By Month) ───────────────
        function syncMonthYearToDateInputs() {
            var m = parseInt($("#monthSelect").val(), 10);
            var y = parseInt($("#yearSelect").val(), 10);
            if (m && y) {
                var firstDate = new Date(y, m - 1, 1);
                var lastDate = new Date(y, m, 0);
                fpFrom.setDate(firstDate);
                fpTo.setDate(lastDate);
            }
        }

        function updateDurationState() {
            var mode = $('input[name="durationRadio"]:checked').val();
            if (mode === "ByDate") {
                $("#fromDateSelect, #toDateSelect").prop("disabled", false).removeClass("disabled-control");
                $("#monthSelect, #yearSelect").prop("disabled", true).addClass("disabled-control");
            } else {
                $("#fromDateSelect, #toDateSelect").prop("disabled", true).addClass("disabled-control");
                $("#monthSelect, #yearSelect").prop("disabled", false).removeClass("disabled-control");
                syncMonthYearToDateInputs();
            }
        }

        $('input[name="durationRadio"]').on("change", updateDurationState);
        $("#monthSelect, #yearSelect").on("change", function () {
            var mode = $('input[name="durationRadio"]:checked').val();
            if (mode === "ByMonth") {
                syncMonthYearToDateInputs();
            }
        });
        updateDurationState();

        // ── Data Loader ──────────────────────────────────────────────
        function loadData(callback) {
            showLoading("Fetching monthly attendance data...");
            $.ajax({
                url: GetSummaryUrl,
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(getFilter()),
                success: function (res) {
                    hideLoading();
                    if (res.success && res.data) {
                        callback(res.data);
                    } else {
                        alert(res.message || 'No data found.');
                    }
                },
                error: function (xhr) {
                    hideLoading();
                    var msg = 'Failed to load data.';
                    try {
                        var err = JSON.parse(xhr.responseText);
                        if (err.message) msg = err.message;
                    } catch (e) { }
                    alert(msg);
                }
            });
        }

        // ── HTML Table Matrix Preview (Identical to Excel) ───────────
        function renderInteractiveGrid(data) {
            if (!data || !data.departmentGroups || data.departmentGroups.length === 0) {
                $('#grid-preview-container').html('<div class="alert alert-warning text-center">No attendance data found for the selected filter.</div>');
                return;
            }

            var summaryHeaders = ["P", "L", "A", "W", "H", "CL", "SL", "UL", "PL", "MRL", "EL", "FL", "ML"];
            var days = data.days || [];

            var html = '';
            html += '<div class="d-flex align-items-center justify-content-between mb-3 pb-2 border-bottom">';
            html += '  <div style="flex: 0 0 160px;">';
            html += '    <img src="/images/DP_logo.png" alt="Company Logo" style="max-height: 52px; max-width: 150px; object-fit: contain;" />';
            html += '  </div>';
            html += '  <div class="text-center flex-grow-1" style="margin-left: -160px;">';
            html += '    <h3 style="font-family:Calibri, sans-serif; font-size:22px; font-weight:bold; margin-bottom:2px; color:#1e293b;">' + (data.header.companyName || "DataPath Ltd.") + '</h3>';
            html += '    <h4 style="font-family:Calibri, sans-serif; font-size:16px; font-weight:bold; margin-bottom:2px; color:#334155;">' + (data.header.reportTitle || "Monthly Attendance Details Report") + '</h4>';
            html += '    <h5 style="font-family:Calibri, sans-serif; font-size:13px; font-weight:600; color:#64748b; margin-bottom:0;">' + (data.header.dateRangeDisplay || data.header.subtitle || "") + '</h5>';
            html += '  </div>';
            html += '  <div style="flex: 0 0 160px;"></div>';
            html += '</div>';

            data.departmentGroups.forEach(function (dept) {
                html += '<div class="table-responsive mb-4" style="box-shadow: 0 1px 3px rgba(0,0,0,0.1); border-radius:4px;">';
                html += '  <table class="monthly-att-table">';

                // Department Header Banner
                var colSpanAll = 4 + days.length + 1 + summaryHeaders.length;
                html += '    <thead>';
                html += '      <tr class="dept-header-row"><td colspan="' + colSpanAll + '">Department: ' + dept.departmentName + '</td></tr>';

                // Table Header Row 1 (Numbers + Names)
                html += '      <tr>';
                html += '        <th rowspan="2" class="header-cell" style="width:35px;">SN</th>';
                html += '        <th rowspan="2" class="header-cell" style="width:110px;">Emp ID</th>';
                html += '        <th rowspan="2" class="header-cell" style="width:220px; text-align:left; padding-left:8px;">Name</th>';
                html += '        <th rowspan="2" class="header-cell" style="width:160px; text-align:left; padding-left:8px;">Designation</th>';

                days.forEach(function (d) {
                    html += '        <th class="header-cell" style="min-width:24px;">' + d.dayNumber + '</th>';
                });

                html += '        <th rowspan="2" class="header-cell" style="width:15px; background-color:#D9EAD3;"></th>';

                summaryHeaders.forEach(function (sh) {
                    html += '        <th rowspan="2" class="header-cell" style="min-width:25px;">' + sh + '</th>';
                });
                html += '      </tr>';

                // Table Header Row 2 (Day Short Names)
                html += '      <tr>';
                days.forEach(function (d) {
                    html += '        <th class="header-cell" style="font-size:9.5px;">' + d.dayName + '</th>';
                });
                html += '      </tr>';
                html += '    </thead>';

                // Body Rows
                html += '    <tbody>';
                (dept.employees || []).forEach(function (emp) {
                    html += '      <tr>';
                    html += '        <td style="font-weight:600;">' + emp.serialNo + '</td>';
                    html += '        <td style="font-family:monospace; font-size:11px;">' + emp.employeeId + '</td>';
                    html += '        <td style="text-align:left; padding-left:8px; font-weight:500;">' + emp.employeeName + '</td>';
                    html += '        <td style="text-align:left; padding-left:8px; color:#475569;">' + (emp.designation || '—') + '</td>';

                    days.forEach(function (d) {
                        var st = (emp.dailyStatuses && emp.dailyStatuses[d.dayNumber]) || '';
                        var cls = '';
                        var stU = st.trim().toUpperCase();
                        if (stU === 'P') cls = 'status-cell-p';
                        else if (stU === 'W') cls = 'status-cell-w';
                        else if (stU === 'L') cls = 'status-cell-l';
                        else if (stU === 'A') cls = 'status-cell-a';
                        else if (stU === 'H') cls = 'status-cell-h';

                        html += '        <td class="' + cls + '" style="font-weight:500;">' + st + '</td>';
                    });

                    // Spacer column
                    html += '        <td style="background-color:#fff;"></td>';

                    // Summary counts
                    var counts = [
                        emp.totalPresent, emp.totalLate, emp.totalAbsent, emp.totalWeekend,
                        emp.totalHoliday, emp.totalCL, emp.totalSL, emp.totalUL,
                        emp.totalPL, emp.totalMRL, emp.totalEL, emp.totalFL, emp.totalML
                    ];
                    counts.forEach(function (c) {
                        html += '        <td style="font-weight:600; color:#1e293b;">' + (c || 0) + '</td>';
                    });

                    html += '      </tr>';
                });

                // Department Subtotal Row
                html += '      <tr class="dept-subtotal-row">';
                html += '        <td colspan="2" style="text-align:left; padding-left:8px;">Total Employees:</td>';
                html += '        <td style="text-align:center; font-size:13px; font-weight:bold; color:#0f172a;">' + dept.totalEmployees + '</td>';
                for (var rem = 4; rem <= colSpanAll; rem++) {
                    html += '        <td></td>';
                }
                html += '      </tr>';

                html += '    </tbody>';
                html += '  </table>';
                html += '</div>';
            });

            $('#grid-preview-container').html(html);
        }

        // ── PDF Preview ──────────────────────────────────────────────
        function previewPdf() {
            showLoading("Generating preview document...");
            $.ajax({
                url: PreviewPdfUrl,
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(getFilter()),
                xhrFields: { responseType: 'blob' },
                success: function (blob) {
                    hideLoading();

                    if (blob.type && blob.type.indexOf('application/json') !== -1) {
                        var reader = new FileReader();
                        reader.onload = function () {
                            try {
                                var err = JSON.parse(reader.result);
                                alert(err.message || 'PDF preview failed!');
                            } catch (e) {
                                alert('PDF preview failed!');
                            }
                        };
                        reader.readAsText(blob);
                        return;
                    }

                    var url = URL.createObjectURL(blob);
                    $('#pdf-preview-container')
                        .html('<iframe src="' + url + '#toolbar=1" width="100%" height="100%" style="border:none;"></iframe>');

                    $('#grid-preview-container').empty();
                    $("#previewWrapper").show();

                    // Activate PDF tab
                    $('#tab-pdf').tab('show');

                    $('html, body').animate({
                        scrollTop: $("#previewWrapper").offset().top - 20
                    }, 300);
                },
                error: function (xhr) {
                    hideLoading();
                    var msg = 'Failed to generate PDF preview.';
                    try {
                        var err = JSON.parse(xhr.responseText);
                        if (err.message) msg = err.message;
                    } catch (e) { }
                    alert(msg);
                }
            });
        }

        // ── PDF Download ─────────────────────────────────────────────
        function downloadPdf() {
            showLoading("Exporting PDF report...");
            $.ajax({
                url: DownloadPdfUrl,
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(getFilter()),
                xhrFields: { responseType: 'blob' },
                success: function (blob, status, xhr) {
                    hideLoading();

                    if (blob.type && blob.type.indexOf('application/json') !== -1) {
                        var reader = new FileReader();
                        reader.onload = function () {
                            try {
                                var err = JSON.parse(reader.result);
                                alert(err.message || 'PDF download failed!');
                            } catch (e) {
                                alert('PDF download failed!');
                            }
                        };
                        reader.readAsText(blob);
                        return;
                    }

                    var cd = xhr.getResponseHeader('Content-Disposition');
                    var ts = new Date().toISOString().replace(/[-T:.Z]/g, '').slice(0, 15);
                    var fileName = 'MonthlyAttendanceDetailsReport_' + ts + '.pdf';
                    if (cd) {
                        var match = cd.match(/filename[^;=\n]*=(['"]?)([^'";\n]+)\1/);
                        if (match && match[2]) fileName = match[2];
                    }

                    var link = document.createElement('a');
                    link.href = URL.createObjectURL(blob);
                    link.download = fileName;
                    document.body.appendChild(link);
                    link.click();
                    document.body.removeChild(link);
                    URL.revokeObjectURL(link.href);
                },
                error: function (xhr) {
                    hideLoading();
                    var msg = 'PDF export failed!';
                    try {
                        var err = JSON.parse(xhr.responseText);
                        if (err.message) msg = err.message;
                    } catch (e) { }
                    alert(msg);
                }
            });
        }

        // ── Excel Download ───────────────────────────────────────────
        function downloadExcel() {
            showLoading("Exporting Excel report...");
            $.ajax({
                url: DownloadExcelUrl,
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(getFilter()),
                xhrFields: { responseType: 'blob' },
                success: function (blob, status, xhr) {
                    hideLoading();

                    if (blob.type && blob.type.indexOf('application/json') !== -1) {
                        var reader = new FileReader();
                        reader.onload = function () {
                            try {
                                var err = JSON.parse(reader.result);
                                alert(err.message || 'Excel download failed!');
                            } catch (e) {
                                alert('Excel download failed!');
                            }
                        };
                        reader.readAsText(blob);
                        return;
                    }

                    var cd = xhr.getResponseHeader('Content-Disposition');
                    var ts = new Date().toISOString().replace(/[-T:.Z]/g, '').slice(0, 15);
                    var fileName = 'MonthlyAttendanceDetailsReport_' + ts + '.xlsx';
                    if (cd) {
                        var match = cd.match(/filename[^;=\n]*=(['"]?)([^'";\n]+)\1/);
                        if (match && match[2]) fileName = match[2];
                    }

                    var link = document.createElement('a');
                    link.href = URL.createObjectURL(blob);
                    link.download = fileName;
                    document.body.appendChild(link);
                    link.click();
                    document.body.removeChild(link);
                    URL.revokeObjectURL(link.href);
                },
                error: function (xhr) {
                    hideLoading();
                    var msg = 'Excel download failed!';
                    try {
                        var err = JSON.parse(xhr.responseText);
                        if (err.message) msg = err.message;
                    } catch (e) { }
                    alert(msg);
                }
            });
        }

        // ── CSV Download ─────────────────────────────────────────────
        function downloadCsv() {
            showLoading("Exporting CSV report...");
            $.ajax({
                url: DownloadCsvUrl,
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(getFilter()),
                xhrFields: { responseType: 'blob' },
                success: function (blob, status, xhr) {
                    hideLoading();

                    if (blob.type && blob.type.indexOf('application/json') !== -1) {
                        var reader = new FileReader();
                        reader.onload = function () {
                            try {
                                var err = JSON.parse(reader.result);
                                alert(err.message || 'CSV download failed!');
                            } catch (e) {
                                alert('CSV download failed!');
                            }
                        };
                        reader.readAsText(blob);
                        return;
                    }

                    var cd = xhr.getResponseHeader('Content-Disposition');
                    var ts = new Date().toISOString().replace(/[-T:.Z]/g, '').slice(0, 15);
                    var fileName = 'MonthlyAttendanceDetailsReport_' + ts + '.csv';
                    if (cd) {
                        var match = cd.match(/filename[^;=\n]*=(['"]?)([^'";\n]+)\1/);
                        if (match && match[2]) fileName = match[2];
                    }

                    var link = document.createElement('a');
                    link.href = URL.createObjectURL(blob);
                    link.download = fileName;
                    document.body.appendChild(link);
                    link.click();
                    document.body.removeChild(link);
                    URL.revokeObjectURL(link.href);
                },
                error: function (xhr) {
                    hideLoading();
                    var msg = 'CSV download failed!';
                    try {
                        var err = JSON.parse(xhr.responseText);
                        if (err.message) msg = err.message;
                    } catch (e) { }
                    alert(msg);
                }
            });
        }

        // ── Button Event Handlers ────────────────────────────────────
        $(document).on("click", "#btnPreviewPdf", function (e) {
            e.preventDefault();
            previewPdf();
        });

        $(document).on("click", "#downloadReport", function (e) {
            e.preventDefault();
            var format = $("#reportFormatSelect").val();
            if (format === "downloadPdf") {
                downloadPdf();
            } else if (format === "downloadExcel") {
                downloadExcel();
            } else if (format === "downloadCsv") {
                downloadCsv();
            } else {
                alert("Please select a valid Export Report Format.");
            }
        });

        $(document).on("shown.bs.tab", "#tab-grid", function () {
            if ($.trim($('#grid-preview-container').html()) === '') {
                loadData(function (data) {
                    renderInteractiveGrid(data);
                });
            }
        });

        $(document).on("click", "#btnClosePreview", function (e) {
            e.preventDefault();
            $("#previewWrapper").slideUp();
        });

        $(document).on("click", "#btnFullscreenPreview", function (e) {
            e.preventDefault();
            var el = document.getElementById("previewTabContent");
            if (!document.fullscreenElement) {
                if (el.requestFullscreen) el.requestFullscreen();
                else if (el.webkitRequestFullscreen) el.webkitRequestFullscreen();
                else if (el.msRequestFullscreen) el.msRequestFullscreen();
                $(this).html('<i class="fas fa-compress"></i> Exit');
            } else {
                if (document.exitFullscreen) document.exitFullscreen();
                $(this).html('<i class="fas fa-expand"></i> Fullscreen');
            }
        });

        // ── Toolbar Actions ──────────────────────────────────────────
        $(document).on("click", "#js-monthly-attendance-clear, .js-monthly-attendance-clear, .js-toolbar-clear, #js-jobcard-bulk-report-clear, .js-jobcard-bulk-report-clear", function () {
            fpFrom.setDate(defaultFromDate);
            fpTo.setDate(defaultToDate);

            $("#byDate").prop("checked", true).trigger("change");
            $("#monthSelect").val(currentMonthNum);
            $("#yearSelect").val(now.getFullYear());
            $("#reportFormatSelect").val("downloadPdf");

            try {
                $("#companySelect").val([]).multiselect("rebuild");
                $("#branchSelect").val([]).multiselect("rebuild");
                $("#departmentSelect").val([]).multiselect("rebuild");
                $("#designationSelect").val([]).multiselect("rebuild");
                $("#employmentNatureSelect").val([]).multiselect("rebuild");
                $("#employeeSelect").val([]).multiselect("rebuild");
            } catch (e) { }

            $("#previewWrapper").hide();
            $("#pdf-preview-container, #grid-preview-container").empty();
        });

        $(document).on("click", ".js-monthly-attendance-print, .js-toolbar-print, .js-jobcard-bulk-report-print, #js-jobcard-bulk-report-print", function () {
            var iframe = document.querySelector("#pdf-preview-container iframe");
            if (iframe && iframe.contentWindow) {
                iframe.contentWindow.print();
            } else {
                previewPdf();
            }
        });

        $(document).on("click", ".js-monthly-attendance-favorite, .js-jobcard-bulk-report-favorite, #js-jobcard-bulk-report-favorite", function () {
            $(this).toggleClass("active");
            if ($(this).hasClass("active")) {
                $(this).find("i").addClass("text-warning");
            } else {
                $(this).find("i").removeClass("text-warning");
            }
        });

        // ── Initialize Remote Cascade / Multi-Select ─────────────────
        if (typeof settings.load === "function") {
            settings.load();
        }
    };
})(jQuery);
