$(document).ready(async function () {

    // ─── REGISTER REMOTE MULTISELECTS (Scroll paging & remote search) ───────
    bindRemoteMultiselect("#companySelect", "/GcAccessFilter/companies", "Select Company", "company");
    bindRemoteMultiselect("#branchSelect", "/GcAccessFilter/branches", "Select Branch", "branch");
    bindRemoteMultiselect("#departmentSelect", "/GcAccessFilter/departments", "Select Department", "department");
    bindRemoteMultiselect("#designationSelect", "/GcAccessFilter/designations", "Select Designation", "designation");
    bindRemoteMultiselect("#employeeSelect", "/GcAccessFilter/employees", "Select Employee", null);

    var accessCode = $("#hdnAccessCode").val();
    var isReadonly = accessCode === "0005";

    if (isReadonly) {
        ms_InitializeMultiselects(null, null, true);
        await ms_ApplyAccessCodeToAll(accessCode);
    } else {
        ms_InitializeMultiselects();
        ms_BindCascade();
        ms_Reset("#companySelect");
        await ms_LoadNext("#companySelect", "/GcAccessFilter/companies");
        await ms_AutoSelectCompany("001");
    }

    // ─── HELPERS ─────────────────────────────────────────────────────────────
    function toArr(val) {
        if (!val) return [];
        return Array.isArray(val) ? val : [val];
    }

    function showToast(type, message) {
        if (typeof toastr !== 'undefined' && toastr[type]) {
            toastr[type](message);
        } else {
            alert(message);
        }
    }

    function setupLoadingOverlay() {
        if ($("#customLoadingOverlay").length === 0) {
            $("body").append(`
                <div id="customLoadingOverlay" style="display:none;position:fixed;top:0;left:0;width:100%;height:100%;background:rgba(0,0,0,0.5);z-index:9999;justify-content:center;align-items:center;">
                    <div style="background:white;padding:20px;border-radius:5px;box-shadow:0 0 10px rgba(0,0,0,0.3);text-align:center;">
                        <div class="spinner-border text-primary"></div>
                        <p style="margin-top:10px;margin-bottom:0;">Loading...</p>
                    </div>
                </div>
            `);
        }
    }

    function showLoading() {
        setupLoadingOverlay();
        $("#customLoadingOverlay").css("display", "flex");
    }

    function hideLoading() {
        $("#customLoadingOverlay").hide();
    }

    function getFilterValue() {
        var compVal = toArr($('#companySelect').val());
        if (compVal.length === 0) compVal = ['001'];

        return {
            CompanyCodes: compVal,
            BranchCodes: toArr($('#branchSelect').val()),
            DepartmentCodes: toArr($('#departmentSelect').val()),
            DesignationCodes: toArr($('#designationSelect').val()),
            EmployeeIDs: toArr($('#employeeSelect').val()),
            Page: 1,
            PageSize: 1000
        };
    }

    // ─── PREVIEW REPORT ──────────────────────────────────────────────────────
    function showPreview() {
        var filterData = getFilterValue();

        $("#leaveSummaryReport-container").show();
        $("#previewLoadingIndicator").show();
        $("#previewIframe").hide();

        $.ajax({
            url: '/HrmLeaveSummaryReport/PreviewReport',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ FilterData: filterData, ExportFormat: 'pdf' }),
            xhrFields: { responseType: 'blob' },
            success: function (data) {
                $("#previewLoadingIndicator").hide();
                if (window.currentPdfUrl) {
                    window.URL.revokeObjectURL(window.currentPdfUrl);
                }
                var blob = new Blob([data], { type: 'application/pdf' });
                window.currentPdfUrl = window.URL.createObjectURL(blob);
                $("#previewIframe").attr('src', window.currentPdfUrl).show();
            },
            error: function (xhr) {
                $("#previewLoadingIndicator").hide();
                $("#leaveSummaryReport-container").hide();
                var msg = 'Preview failed. Please try again.';
                if (xhr.status === 404) {
                    msg = 'No data found for the selected criteria.';
                } else if (xhr.status === 400) {
                    msg = 'Invalid request. Please check your inputs.';
                }
                showToast('warning', msg);
            }
        });
    }

    function hidePreview() {
        $("#leaveSummaryReport-container").hide();
        $("#previewIframe").attr('src', '').hide();
        if (window.currentPdfUrl) {
            window.URL.revokeObjectURL(window.currentPdfUrl);
            window.currentPdfUrl = null;
        }
    }

    // ─── EXPORT REPORT ───────────────────────────────────────────────────────
    function exportReport(format) {
        format = format || $('#exportFormatSelect').val() || 'pdf';
        var filterData = getFilterValue();

        showLoading();

        $.ajax({
            url: '/HrmLeaveSummaryReport/ExportReport',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ FilterData: filterData, ExportFormat: format }),
            xhrFields: { responseType: 'blob' },
            success: function (data, status, xhr) {
                hideLoading();

                var contentType = xhr.getResponseHeader('Content-Type') || 'application/pdf';
                var disposition = xhr.getResponseHeader('Content-Disposition');

                var ts = new Date().toISOString().replace(/[-:T.]/g, '').slice(0, 14);
                var filename = 'LeaveSummaryReport_' + ts;
                if (format === 'excel') filename += '.xlsx';
                else if (format === 'word') filename += '.docx';
                else filename += '.pdf';

                if (disposition && disposition.indexOf("filename=") !== -1) {
                    var match = disposition.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/);
                    if (match && match[1]) {
                        filename = match[1].replace(/['"]/g, '');
                    }
                }

                if (format === "download") {
                    var blob = new Blob([data], { type: 'application/pdf' });
                    var iframe = Object.assign(document.createElement('iframe'), {
                        style: 'display:none',
                        src: window.URL.createObjectURL(blob),
                        onload: function () {
                            this.contentWindow.focus();
                            this.contentWindow.print();
                        }
                    });
                    document.body.appendChild(iframe);
                    return;
                }

                var blob = new Blob([data], { type: contentType });
                var link = Object.assign(document.createElement("a"), {
                    href: window.URL.createObjectURL(blob),
                    download: filename
                });

                document.body.appendChild(link);
                link.click();
                document.body.removeChild(link);
                window.URL.revokeObjectURL(link.href);
                showToast('success', 'Report exported successfully');
            },
            error: function (e) {
                hideLoading();
                var msg = 'Export failed. Please try again.';
                if (e.status === 404) {
                    msg = 'No data found for the selected criteria.';
                } else if (e.status === 400) {
                    msg = 'Invalid request. Please check your inputs.';
                }
                showToast('warning', msg);
            }
        });
    }

    // ─── CLEAR / RESET ───────────────────────────────────────────────────────
    async function clearAllFilters() {
        ['#branchSelect', '#departmentSelect', '#designationSelect', '#employeeSelect'].forEach(function (sel) {
            try {
                $(sel).multiselect('deselectAll', false);
                $(sel).multiselect('updateButtonText');
            } catch (e) { }
        });

        ms_Reset("#companySelect");
        await ms_LoadNext("#companySelect", "/GcAccessFilter/companies");
        await ms_AutoSelectCompany("001");

        $('#exportFormatSelect').val('pdf');
        hidePreview();
    }

    // ─── EVENT HANDLERS ──────────────────────────────────────────────────────
    $('#previewBtn').on('click', function (e) {
        e.preventDefault();
        if ($("#leaveSummaryReport-container").is(':visible')) {
            hidePreview();
        } else {
            showPreview();
        }
    });

    $('#downloadBtn').on('click', function (e) {
        e.preventDefault();
        exportReport($('#exportFormatSelect').val());
    });

    // Toolbar buttons triggering action buttons
    $(document).on('click', '#downloadReport, .js-leveSummery', function (e) {
        e.preventDefault();
        $('#downloadBtn').trigger('click');
    });

    $(document).on('click', '#btnPreviewPdf', function (e) {
        e.preventDefault();
        $('#previewBtn').trigger('click');
    });

    $('#closePreviewBtn').on('click', hidePreview);
    $('#refreshPreviewBtn').on('click', showPreview);
    $('#btnClear, #js-leveSummery-report-clear').on('click', clearAllFilters);

    $('.js-HomeOfficeRequest-report-favorite').on('click', function () {
        toastr.success('Favorite button Coming soon!');
    });

    // Default export format
    $('#exportFormatSelect').val('pdf');
});