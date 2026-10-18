// ═══════════════════════════════════════════════════════════════════
// File: wwwroot/js/pages/JobCardBulkReport.js
// ═══════════════════════════════════════════════════════════════════
(function ($) {
    $.jobCardBulkReport = function (options) {
        var settings = $.extend({ baseUrl: "/JobCardBulkReport" }, options);

        var base = (settings.baseUrl || "/JobCardBulkReport").replace(/\/Index\/?$/i, '').replace(/\/+$/, '');
        if (!base) base = "/JobCardBulkReport";

        var GetSummaryUrl    = base + "/GetSummary";
        var DownloadExcelUrl = base + "/DownloadExcel";
        var DownloadCsvUrl   = base + "/DownloadCsv";
        var PreviewPdfUrl    = base + "/PreviewPdf";
        var DownloadPdfUrl   = base + "/DownloadPdf";

        // ── Loading Overlay ───────────────────────────────────────────
        (function setupOverlay() {
            if ($("#bulkReportLoadingOverlay").length === 0) {
                $("body").append(`
                    <div id="bulkReportLoadingOverlay" style="
                        display:none;position:fixed;top:0;left:0;
                        width:100%;height:100%;background:rgba(15,23,42,0.65);
                        backdrop-filter:blur(4px);-webkit-backdrop-filter:blur(4px);
                        z-index:999999;justify-content:center;align-items:center;">
                        <div style="background:#fff;padding:24px 36px;border-radius:10px;
                                    box-shadow:0 10px 25px rgba(0,0,0,0.25);text-align:center;min-width:200px;">
                            <div class="spinner-border text-primary" style="width:3rem;height:3rem;" role="status">
                                <span class="sr-only">Loading...</span>
                            </div>
                            <h6 id="bulkReportLoadingMsg" style="margin-top:14px;margin-bottom:0;color:#1e293b;font-weight:600;">Processing Job Card Bulk Report...</h6>
                            <small style="color:#64748b;">Please wait a moment</small>
                        </div>
                    </div>`);
            }
        })();

        function showLoading(msg) {
            if (msg) $("#bulkReportLoadingMsg").text(msg);
            else $("#bulkReportLoadingMsg").text("Processing Job Card Bulk Report...");
            $("#bulkReportLoadingOverlay").css("display", "flex");
        }

        function hideLoading() {
            $("#bulkReportLoadingOverlay").hide();
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
            var compVal = $("#companySelect").val();
            var compArr = getMultiVal("#companySelect");
            var compCode = compArr && compArr.length > 0 ? compArr[0] : null;

            var durationType = $('input[name="durationRadio"]:checked').val() || "ByDate";

            return {
                CompanyCode: compCode,
                CompanyCodes: compArr,
                DepartmentCodes: getMultiVal("#departmentSelect"),
                DesignationCodes: getMultiVal("#designationSelect"),
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
        var defaultToDate = new Date(); // Today

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
        if ($("#monthSelect").val() === "") {
            $("#monthSelect").val(currentMonthNum);
        }

        // ── Duration Radio Toggle (By Date vs By Month) ───────────────
        function updateDurationState() {
            var mode = $('input[name="durationRadio"]:checked').val();
            if (mode === "ByDate") {
                $("#fromDateSelect, #toDateSelect").prop("disabled", false).removeClass("disabled-control");
                $("#monthSelect, #yearSelect").prop("disabled", true).addClass("disabled-control");
            } else {
                $("#fromDateSelect, #toDateSelect").prop("disabled", true).addClass("disabled-control");
                $("#monthSelect, #yearSelect").prop("disabled", false).removeClass("disabled-control");
            }
        }

        $('input[name="durationRadio"]').on("change", updateDurationState);
        updateDurationState();

        // ── Data Loader ──────────────────────────────────────────────
        function loadData(callback) {
            showLoading("Fetching bulk job card data...");
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

        // ── Helpers ──────────────────────────────────────────────────
        function formatPrintDT() {
            var d = new Date();
            var dd = String(d.getDate()).padStart(2, '0');
            var mm = String(d.getMonth() + 1).padStart(2, '0');
            var yy = d.getFullYear();
            var h = d.getHours();
            var min = String(d.getMinutes()).padStart(2, '0');
            var sec = String(d.getSeconds()).padStart(2, '0');
            var ap = h >= 12 ? 'PM' : 'AM';
            h = h % 12 || 12;
            return dd + '/' + mm + '/' + yy + ' ' + String(h).padStart(2, '0') + ':' + min + ':' + sec + ' ' + ap;
        }

        function getImageBase64(url, cb) {
            var img = new Image();
            img.crossOrigin = 'Anonymous';
            img.onload = function () {
                var c = document.createElement('canvas');
                c.width = img.naturalWidth;
                c.height = img.naturalHeight;
                c.getContext('2d').drawImage(img, 0, 0);
                cb(c.toDataURL('image/png'), img.naturalWidth, img.naturalHeight);
            };
            img.onerror = function () { cb(null, 0, 0); };
            img.src = url;
        }

        function getStatusColor(status) {
            var s = (status || '').trim();
            if (s === 'P') return [40, 167, 69];    // Green
            if (s === 'L') return [200, 145, 8];    // Amber
            if (s === 'A') return [220, 53, 69];    // Red
            return [0, 0, 0];                       // Black
        }

        // ── PDF Builder (Grouped by Department, Then Employee) ────────
        function buildPdf(data, forPreview) {
            var employeeGroups = [];

            // Grouping: Department finishes first, then next department starts
            if (data.departmentGroups && data.departmentGroups.length > 0) {
                data.departmentGroups.forEach(function (dept) {
                    if (dept.employees && dept.employees.length > 0) {
                        dept.employees.forEach(function (emp) {
                            emp.departmentName = emp.departmentName || dept.departmentName;
                            employeeGroups.push(emp);
                        });
                    }
                });
            } else if (data.employeeGroups && data.employeeGroups.length > 0) {
                employeeGroups = data.employeeGroups;
            } else if (data.rows && data.rows.length > 0) {
                // Fallback group by employee
                var map = {};
                data.rows.forEach(function (r) {
                    if (!map[r.employeeId]) {
                        map[r.employeeId] = {
                            employeeId: r.employeeId,
                            employeeName: r.employeeName,
                            designation: r.designation,
                            departmentName: r.departmentName,
                            companyName: r.companyName,
                            rows: []
                        };
                        employeeGroups.push(map[r.employeeId]);
                    }
                    map[r.employeeId].rows.push(r);
                });
            }

            if (employeeGroups.length === 0) {
                alert("No records found for the selected filter.");
                return;
            }

            getImageBase64('/images/DP_logo.png', function (logoBase64, logoW, logoH) {
                var { jsPDF } = window.jspdf;
                var doc = new jsPDF({
                    orientation: 'portrait',
                    unit: 'pt',
                    format: 'a4'
                });

                var PW = doc.internal.pageSize.getWidth();
                var PH = doc.internal.pageSize.getHeight();
                var LEFT_MARGIN = 20;
                var RIGHT_MARGIN = 20;
                var usableWidth = PW - LEFT_MARGIN - RIGHT_MARGIN;
                var printDT = formatPrintDT();

                var companyName = (data.header && data.header.companyName) || 'DataPath Ltd.';
                var dateRange = (data.header && data.header.dateRangeDisplay) || ('Date: ' + $("#fromDateSelect").val() + ' - ' + $("#toDateSelect").val());

                function drawHeader() {
                    // Logo at Top-Left
                    if (logoBase64) {
                        try {
                            var aspect = logoH > 0 ? logoW / logoH : 2.5;
                            var renderH = 22;
                            var renderW = renderH * aspect;
                            doc.addImage(logoBase64, 'PNG', LEFT_MARGIN, 22, renderW, renderH);
                        } catch (e) { }
                    }

                    // Company Name (Centered)
                    doc.setFont('times', 'bold');
                    doc.setFontSize(12);
                    doc.setTextColor(0, 0, 0);
                    doc.text(companyName, PW / 2, 28, { align: 'center' });

                    // Report Subtitle (Centered)
                    doc.setFont('times', 'bold');
                    doc.setFontSize(9.5);
                    doc.text('Job Card Report', PW / 2, 42, { align: 'center' });

                    // Date Range (Centered)
                    doc.setFont('times', 'normal');
                    doc.setFontSize(8.5);
                    doc.text(dateRange, PW / 2, 55, { align: 'center' });
                }

                function drawFooter(pageNo) {
                    var footerY = PH - 18;
                    doc.setFont('times', 'normal');
                    doc.setFontSize(7.5);
                    doc.setTextColor(0, 0, 0);

                    // Print Datetime (Left)
                    doc.text('Print Datetime: ' + printDT, LEFT_MARGIN, footerY);

                    // Page X of Y (Right)
                    var pageStr = 'Page ' + pageNo + ' of {total_pages_count_string}';
                    doc.text(pageStr, PW - RIGHT_MARGIN, footerY, { align: 'right' });
                }

                function drawEmployeeInfoBlock(emp, startY) {
                    doc.setFont('times', 'normal');
                    doc.setFontSize(8);
                    doc.setTextColor(0, 0, 0);

                    var labelX = LEFT_MARGIN;
                    var colonX = labelX + 42;
                    var valX = colonX + 3;

                    // Row 1: Emp. ID
                    doc.setFont('times', 'bold');
                    doc.text('Emp. ID', labelX, startY);
                    doc.text(':', colonX, startY);
                    doc.setFont('times', 'normal');
                    doc.text(emp.employeeId || '', valX, startY);

                    // Row 2: Name
                    doc.setFont('times', 'bold');
                    doc.text('Name', labelX, startY + 12);
                    doc.text(':', colonX, startY + 12);
                    doc.setFont('times', 'normal');
                    doc.text(emp.employeeName || '', valX, startY + 12);

                    // Row 3: Designation
                    doc.setFont('times', 'bold');
                    doc.text('Designation', labelX, startY + 24);
                    doc.text(':', colonX, startY + 24);
                    doc.setFont('times', 'normal');
                    doc.text(emp.designation || '', valX, startY + 24);

                    // Row 4: Department
                    doc.setFont('times', 'bold');
                    doc.text('Department', labelX, startY + 36);
                    doc.text(':', colonX, startY + 36);
                    doc.setFont('times', 'normal');
                    doc.text(emp.departmentName || '', valX, startY + 36);

                    return startY + 44;
                }

                var isFirstEmployee = true;

                employeeGroups.forEach(function (emp) {
                    if (!isFirstEmployee) {
                        doc.addPage();
                    }
                    isFirstEmployee = false;

                    var tableStartY = drawEmployeeInfoBlock(emp, 72);

                    var bodyRows = (emp.rows || []).map(function (r) {
                        return [
                            r.dateDisplay || '',
                            r.day || '',
                            r.shift || '',
                            r.inTime || '',
                            r.late || '00:00:00',
                            r.outTime || '',
                            r.earlyOut || '00:00:00',
                            r.workHours || '00:00:00',
                            r.status || '',
                            r.remarks || ''
                        ];
                    });

                    doc.autoTable({
                        startY: tableStartY,
                        tableWidth: usableWidth,
                        margin: { left: LEFT_MARGIN, right: RIGHT_MARGIN, top: 65, bottom: 28 },
                        theme: 'grid',
                        head: [[
                            'Date',
                            'Day',
                            'Shift',
                            'In Time',
                            'Late',
                            'Out Time',
                            'Early Out',
                            'W. Hour(s)',
                            'Status',
                            'Remarks'
                        ]],
                        body: bodyRows,
                        styles: {
                            font: 'times',
                            fontSize: 6.5,
                            cellPadding: { top: 2.2, right: 2, bottom: 2.2, left: 2 },
                            halign: 'center',
                            valign: 'middle',
                            textColor: [0, 0, 0],
                            lineColor: [210, 210, 210],
                            lineWidth: 0.25,
                            fillColor: [255, 255, 255]
                        },
                        headStyles: {
                            font: 'times',
                            fontStyle: 'bold',
                            halign: 'center',
                            valign: 'middle',
                            fontSize: 7,
                            textColor: [0, 0, 0],
                            lineColor: [180, 180, 180],
                            lineWidth: 0.25,
                            fillColor: [255, 255, 255]
                        },
                        columnStyles: {
                            0: { cellWidth: 46 }, // Date
                            1: { cellWidth: 46 }, // Day
                            2: { cellWidth: 105 }, // Shift
                            3: { cellWidth: 62 }, // In Time
                            4: { cellWidth: 46 }, // Late
                            5: { cellWidth: 62 }, // Out Time
                            6: { cellWidth: 46 }, // Early Out
                            7: { cellWidth: 48 }, // W. Hour(s)
                            8: { cellWidth: 32 }, // Status
                            9: { cellWidth: 'auto', halign: 'left' } // Remarks
                        },
                        didParseCell: function (cellData) {
                            if (cellData.section === 'body') {
                                if (cellData.column.index === 9) {
                                    cellData.cell.styles.halign = 'left';
                                }
                            }
                        },
                        willDrawCell: function (cellData) {
                            if (cellData.section === 'body' && cellData.column.index === 8) {
                                var statusVal = cellData.cell.raw;
                                var color = getStatusColor(statusVal);
                                doc.setTextColor(color[0], color[1], color[2]);
                                cellData.cell.styles.fontStyle = 'bold';
                            }
                        },
                        didDrawPage: function () {
                            drawHeader();
                            drawFooter(doc.internal.getCurrentPageInfo().pageNumber);
                        }
                    });

                    // ── Bottom Summary & Stats Section ───────────────────────
                    var boxH = 62;
                    var boxTopY = PH - 130;

                    if (doc.lastAutoTable.finalY > boxTopY - 10) {
                        doc.addPage();
                        drawHeader();
                        drawFooter(doc.internal.getCurrentPageInfo().pageNumber);
                    }

                    // Calculate stats for this employee
                    var totalRows = (emp.rows || []).length;
                    var presentCount = 0;
                    var lateCount = 0;
                    var absentCount = 0;
                    var weekendCount = 0;
                    var holidayCount = 0;
                    var leaveCount = 0;
                    var earlyOutCount = 0;

                    var totalLateSec = 0;
                    var totalWorkSec = 0;

                    (emp.rows || []).forEach(function (r) {
                        var st = (r.status || '').trim();
                        var lt = (r.late || '').trim();
                        var eo = (r.earlyOut || '').trim();
                        var wh = (r.workHours || '').trim();

                        if (st === 'P') presentCount++;
                        else if (st === 'L') lateCount++;
                        else if (st === 'A') absentCount++;
                        else if (st === 'W') weekendCount++;
                        else if (st === 'H') holidayCount++;
                        else leaveCount++;

                        if (st === 'L' && lt && lt !== '00:00:00' && lt !== '0.00' && lt !== '0') {
                            if (lt.indexOf(':') !== -1) {
                                var lp = lt.split(':');
                                if (lp.length === 3) {
                                    totalLateSec += (parseInt(lp[0], 10) * 3600) + (parseInt(lp[1], 10) * 60) + parseInt(lp[2], 10);
                                } else if (lp.length === 2) {
                                    totalLateSec += (parseInt(lp[0], 10) * 3600) + (parseInt(lp[1], 10) * 60);
                                }
                            } else {
                                var decL = parseFloat(lt);
                                if (!isNaN(decL)) {
                                    totalLateSec += Math.round(decL * 3600);
                                }
                            }
                        }

                        if (eo && eo !== '00:00:00' && eo !== '0.00' && eo !== '0' && st !== 'W' && st !== 'H' && st !== 'A') {
                            earlyOutCount++;
                        }

                        if (wh && wh !== '00:00:00' && wh !== '0.00' && wh !== '0') {
                            if (wh.indexOf(':') !== -1) {
                                var wp = wh.split(':');
                                if (wp.length === 3) {
                                    totalWorkSec += (parseInt(wp[0], 10) * 3600) + (parseInt(wp[1], 10) * 60) + parseInt(wp[2], 10);
                                } else if (wp.length === 2) {
                                    totalWorkSec += (parseInt(wp[0], 10) * 3600) + (parseInt(wp[1], 10) * 60);
                                }
                            } else {
                                var decW = parseFloat(wh);
                                if (!isNaN(decW)) {
                                    totalWorkSec += Math.round(decW * 3600);
                                }
                            }
                        }
                    });

                    function formatDurationStr(sec) {
                        var h = Math.floor(sec / 3600);
                        var rem = sec % 3600;
                        var m = Math.floor(rem / 60);
                        var s = rem % 60;
                        return h + ':' + String(m).padStart(2, '0') + ':' + String(s).padStart(2, '0');
                    }

                    var totalPresent = presentCount + lateCount;
                    var totalWorkingDays = totalRows - weekendCount - holidayCount;
                    var totalAtt = totalPresent + weekendCount + holidayCount + leaveCount;

                    var totalLateStr = formatDurationStr(totalLateSec);
                    var totalWorkStr = formatDurationStr(totalWorkSec);

                    var avgTimeStr = "0:00";
                    if (totalPresent > 0) {
                        var avgSec = Math.round(totalWorkSec / totalPresent);
                        var avgH = Math.floor(avgSec / 3600);
                        var avgM = Math.floor((avgSec % 3600) / 60);
                        avgTimeStr = avgH + ':' + String(avgM).padStart(2, '0');
                    }

                    // 1. Draw Border around Attendance Summary Box
                    var boxX = LEFT_MARGIN;
                    var boxW = 194;

                    doc.setDrawColor(180, 180, 180);
                    doc.setLineWidth(0.5);
                    doc.rect(boxX, boxTopY, boxW, boxH);

                    // 2. Attendance Summary Statistics Text Inside Border
                    doc.setFontSize(7);
                    doc.setTextColor(0, 0, 0);

                    // Row 1
                    doc.setFont('times', 'bold');
                    doc.text('Total Working Days', boxX + 6, boxTopY + 11);
                    doc.setFont('times', 'normal');
                    doc.text(String(totalWorkingDays), boxX + 90, boxTopY + 11, { align: 'right' });

                    doc.setFont('times', 'bold');
                    doc.text('Total Weekend:', boxX + 100, boxTopY + 11);
                    doc.setFont('times', 'normal');
                    doc.text(String(weekendCount), boxX + 186, boxTopY + 11, { align: 'right' });

                    // Row 2
                    doc.setFont('times', 'bold');
                    doc.text('Total Present:', boxX + 25, boxTopY + 22);
                    doc.setFont('times', 'normal');
                    doc.text(String(totalPresent), boxX + 90, boxTopY + 22, { align: 'right' });

                    doc.setFont('times', 'bold');
                    doc.text('Total Holiday:', boxX + 105, boxTopY + 22);
                    doc.setFont('times', 'normal');
                    doc.text(String(holidayCount), boxX + 186, boxTopY + 22, { align: 'right' });

                    // Row 3
                    doc.setFont('times', 'bold');
                    doc.text('Total Absent:', boxX + 26, boxTopY + 33);
                    doc.setFont('times', 'normal');
                    doc.text(String(absentCount), boxX + 90, boxTopY + 33, { align: 'right' });

                    doc.setFont('times', 'bold');
                    doc.text('Total Early Out:', boxX + 98, boxTopY + 33);
                    doc.setFont('times', 'normal');
                    doc.text(String(earlyOutCount), boxX + 186, boxTopY + 33, { align: 'right' });

                    // Row 4
                    doc.setFont('times', 'bold');
                    doc.text('Total Late:', boxX + 34, boxTopY + 44);
                    doc.setFont('times', 'normal');
                    doc.text(String(lateCount), boxX + 90, boxTopY + 44, { align: 'right' });

                    doc.setFont('times', 'bold');
                    doc.text('Total Att.:', boxX + 117, boxTopY + 44);
                    doc.setFont('times', 'normal');
                    doc.text(String(totalAtt), boxX + 186, boxTopY + 44, { align: 'right' });

                    // Row 5
                    doc.setFont('times', 'bold');
                    doc.text('Total Leave:', boxX + 30, boxTopY + 55);
                    doc.setFont('times', 'normal');
                    doc.text(String(leaveCount), boxX + 90, boxTopY + 55, { align: 'right' });

                    // 3. Column Totals Beside the Box
                    doc.setFont('times', 'bold');
                    doc.setFontSize(8);
                    doc.setTextColor(0, 0, 0);

                    doc.text(totalLateStr, 302, boxTopY + 11, { align: 'center' });
                    doc.text(totalWorkStr, 457, boxTopY + 11, { align: 'center' });
                    doc.text('Average Time:     ' + avgTimeStr, 380, boxTopY + 44);

                    // 4. Signatures
                    var sigLineY = PH - 58;
                    var sigTextY = PH - 48;

                    doc.setDrawColor(0, 0, 0);
                    doc.setLineWidth(0.6);

                    doc.line(LEFT_MARGIN, sigLineY, LEFT_MARGIN + 95, sigLineY);
                    doc.setFont('times', 'normal');
                    doc.setFontSize(7.5);
                    doc.text('Prepared by', LEFT_MARGIN + 47.5, sigTextY, { align: 'center' });

                    doc.line(PW / 2 - 47.5, sigLineY, PW / 2 + 47.5, sigLineY);
                    doc.text('Checked by', PW / 2, sigTextY, { align: 'center' });

                    doc.line(PW - RIGHT_MARGIN - 95, sigLineY, PW - RIGHT_MARGIN, sigLineY);
                    doc.text('Authorized by', PW - RIGHT_MARGIN - 47.5, sigTextY, { align: 'center' });

                    // 5. Status Legend
                    var legendLine1Y = PH - 35;
                    var legendLine2Y = PH - 27;

                    doc.setFont('times', 'normal');
                    doc.setFontSize(6.5);
                    doc.setTextColor(0, 0, 0);
                    doc.text('Status Legend: P-Present, L- Late, A- Absent, W- Weekend, H- Holiday, CL- Casual Leave, SL- Sick Leave, UL- Unpaid Leave, ML- Maternity Leave,', PW / 2, legendLine1Y, { align: 'center' });
                    doc.text('PL- Paternity Leave, MarL- Marriage Leave, HL- Hajj Leave, UmrL- Umrah Leave', PW / 2, legendLine2Y, { align: 'center' });
                });

                if (typeof doc.putTotalPages === 'function') {
                    doc.putTotalPages('{total_pages_count_string}');
                }

                var ts = new Date().toISOString().replace(/[-T:.Z]/g, '').slice(0, 15);
                var fileName = 'JobCardBulkReport_' + ts + '.pdf';

                if (forPreview) {
                    var blob = doc.output('blob');
                    var url = URL.createObjectURL(blob);
                    $('#pdf-preview-container')
                        .html('<iframe src="' + url + '#toolbar=1" width="100%" height="100%" style="border:none;"></iframe>')
                        .show();

                    $('html, body').animate({
                        scrollTop: $("#pdf-preview-container").offset().top - 20
                    }, 300);
                } else {
                    doc.save(fileName);
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
                    var fileName = 'JobCardBulkReport_' + ts + '.xlsx';
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
                    var fileName = 'JobCardBulkReport_' + ts + '.csv';
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
        $(document).on("click", "#btnPreviewPdf", function () {
            loadData(function (data) {
                buildPdf(data, true);
            });
        });

        $(document).on("click", "#downloadReport", function () {
            var format = $("#reportFormatSelect").val();
            if (format === "downloadPdf") {
                loadData(function (data) {
                    buildPdf(data, false);
                });
            } else if (format === "downloadExcel") {
                downloadExcel();
            } else if (format === "downloadCsv") {
                downloadCsv();
            } else {
                alert("Please select a valid Export Format.");
            }
        });

        // ── Toolbar Actions ──────────────────────────────────────────
        $(document).on("click", "#js-jobcard-bulk-report-clear", function () {
            fpFrom.setDate(defaultFromDate);
            fpTo.setDate(defaultToDate);

            $("#byDate").prop("checked", true).trigger("change");
            $("#monthSelect").val(currentMonthNum);
            $("#yearSelect").val(now.getFullYear());

            try {
                $("#departmentSelect").val([]).multiselect("rebuild");
                $("#designationSelect").val([]).multiselect("rebuild");
                $("#employeeSelect").val([]).multiselect("rebuild");
            } catch (e) { }

            $("#pdf-preview-container").hide().empty();
        });

        $(document).on("click", ".js-jobcard-bulk-report-print", function () {
            var iframe = document.querySelector("#pdf-preview-container iframe");
            if (iframe && iframe.contentWindow) {
                iframe.contentWindow.print();
            } else {
                loadData(function (data) {
                    buildPdf(data, true);
                });
            }
        });

        // ── Initialize Remote Cascade / Multi-Select ─────────────────
        if (typeof settings.load === "function") {
            settings.load();
        }
    };
})(jQuery);
