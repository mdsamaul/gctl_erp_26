$(document).ready(async function () {

    // ─── REGISTER REMOTE MULTISELECTS (Scroll paging & remote search) ───────
    bindRemoteMultiselect("#companySelect", "/GcAccessFilter/companies", "Select Company", "company");
    bindRemoteMultiselect("#branchSelect", "/GcAccessFilter/branches", "Select Branch", "branch");
    bindRemoteMultiselect("#departmentSelect", "/GcAccessFilter/departments", "Select Department", "department");
    bindRemoteMultiselect("#employeeSelect", "/GcAccessFilter/employees", "Select Employee", null);

    var accessCode = $("#hdnAccessCode").val();
    var isReadonly = accessCode === "0005";

    // Initialize static multiselects with matching design & inline search
    ms_InitializeMultiselects({
        '#leaveFormatDropdown': 'Select Leave Format',
        '#leaveStatusDropdown': 'Select Leave Status'
    });

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

    // Client-side search for static dropdowns (leave format & leave status)
    $(document).on('input', '.multiselect-inline-search', function () {
        const $btn = $(this).closest('button.multiselect');
        const $container = $btn.next('.multiselect-container');
        const $select = $btn.parent().prev('select');
        if ($select.length && (!filterUrlMap || !filterUrlMap.has('#' + $select.attr('id')))) {
            const val = ($(this).val() || '').toLowerCase().trim();
            $container.find('li:not(.multiselect-item)').each(function () {
                const text = $(this).text().toLowerCase();
                $(this).toggle(text.indexOf(val) > -1);
            });
        }
    });

    $(document).on('hidden.bs.dropdown', '.btn-group', function () {
        $(this).find('ul.multiselect-container li').show();
    });

    // ─── DATEPICKER ──────────────────────────────────────────────────────────
    function getCurrentDate() {
        var today = new Date();
        return String(today.getDate()).padStart(2, '0') + '/'
             + String(today.getMonth() + 1).padStart(2, '0') + '/'
             + today.getFullYear();
    }

    function initDatePicker() {
        if (typeof CalendarService !== 'undefined' && typeof flatpickr !== 'undefined') {
            flatpickr($("#dateFromStr, #dateToStr"), CalendarService.createConfig({
                defaultDate: new Date()
            }));
        } else if (typeof flatpickr !== 'undefined') {
            flatpickr("#dateFromStr, #dateToStr", {
                dateFormat: "d/m/Y",
                defaultDate: new Date()
            });
        } else {
            $('#dateFromStr, #dateToStr').val(getCurrentDate());
        }
    }

    initDatePicker();

    function toArr(val) {
        if (!val) return [];
        return Array.isArray(val) ? val : [val];
    }

    // ─── CLEAR / RESET ───────────────────────────────────────────────────────
    $('#btnClear, #js-leveSummery-report-clear').on('click', async function () {
        ['#branchSelect', '#departmentSelect', '#employeeSelect', '#leaveFormatDropdown', '#leaveStatusDropdown'].forEach(function (sel) {
            try {
                $(sel).multiselect('deselectAll', false);
                $(sel).multiselect('updateButtonText');
            } catch (e) { }
        });

        ms_Reset("#companySelect");
        await ms_LoadNext("#companySelect", "/GcAccessFilter/companies");
        await ms_AutoSelectCompany("001");

        $('#reportFormatDropdown').val('pdf');
        initDatePicker();

        $('#pdfPreviewContainer').hide();
        $('#pdfPreview').attr('src', '');
        $('#leaveContainer').empty();
    });

    // ─── TOOLBAR BUTTONS ─────────────────────────────────────────────────────
    $(document).on('click', '#downloadReport, .js-leveSummery', function (e) {
        e.preventDefault();
        $('#exportButtonNew').trigger('click');
    });

    $(document).on('click', '#btnPreviewPdf', function (e) {
        e.preventDefault();
        $('#previewBtn2').trigger('click');
    });

    $('.js-HomeOfficeRequest-report-favorite').on('click', function () {
        toastr.success('Favorite button Coming soon!');
    });

    // ─── PREVIEW PDF ─────────────────────────────────────────────────────────
    $('#previewBtn2').on('click', function (e) {
        e.preventDefault();

        var companyVal = toArr($('#companySelect').val());
        if (companyVal.length === 0) companyVal = ['001'];

        var formData = {
            company: companyVal,
            branch: toArr($('#branchSelect').val()),
            department: toArr($('#departmentSelect').val()),
            employee: toArr($('#employeeSelect').val()),
            dateFromStr: $('#dateFromStr').val(),
            dateToStr: $('#dateToStr').val(),
            leaveFormat: toArr($('#leaveFormatDropdown').val()),
            leaveStatus: toArr($('#leaveStatusDropdown').val())
        };

        $('#loadingSpinner1').show();
        $('#pdfPreviewContainer').hide();

        $.ajax({
            type: 'POST',
            url: '/LeaveReport/PreviewPdf',
            data: formData,
            traditional: true,
            success: function (response) {
                $('#loadingSpinner1').hide();
                if (!response.success) {
                    toastr.warning(response.message || 'Failed to generate preview.');
                    return;
                }
                $('#pdfPreview').attr('src', 'data:application/pdf;base64,' + response.pdfData);
                $('#pdfPreviewContainer').show();
                $('html, body').animate({ scrollTop: $('#pdfPreviewContainer').offset().top - 50 }, 500);
            },
            error: function (xhr) {
                $('#loadingSpinner1').hide();
                var msg = xhr.responseJSON?.message || 'An error occurred while generating preview.';
                toastr.warning(msg);
            }
        });
    });

    // ─── EXPORT REPORT ───────────────────────────────────────────────────────
    $('#exportButtonNew').on('click', function (e) {
        e.preventDefault();

        var selectedFormat = $('#reportFormatDropdown').val() || 'pdf';
        if (selectedFormat === 'downloadPdf') selectedFormat = 'pdf';
        if (selectedFormat === 'downloadExcel') selectedFormat = 'excel';

        var companyVal = toArr($('#companySelect').val());
        if (companyVal.length === 0) companyVal = ['001'];

        var formData = {
            company: companyVal,
            branch: toArr($('#branchSelect').val()),
            department: toArr($('#departmentSelect').val()),
            employee: toArr($('#employeeSelect').val()),
            dateFromStr: $('#dateFromStr').val(),
            dateToStr: $('#dateToStr').val(),
            leaveFormat: toArr($('#leaveFormatDropdown').val()),
            leaveStatus: toArr($('#leaveStatusDropdown').val()),
            reportFormat: selectedFormat
        };

        toastr.info('Generating report...', 'Please wait', { timeOut: 3000, closeButton: true });
        $('#loadingSpinner1').show();

        $.ajax({
            type: 'POST',
            url: '/LeaveReport/DownloadReportN',
            data: formData,
            traditional: true,
            xhrFields: { responseType: 'blob' },
            success: function (blob, status, xhr) {
                $('#loadingSpinner1').hide();

                var contentType = xhr.getResponseHeader('Content-Type') || '';
                var contentDisposition = xhr.getResponseHeader('Content-Disposition') || '';
                var filename = 'LeaveReport';

                if (contentType.includes('application/json')) {
                    var reader = new FileReader();
                    reader.onload = function () {
                        try {
                            var res = JSON.parse(reader.result);
                            toastr.warning(res.message || 'Could not generate report.');
                        } catch (ex) {
                            toastr.warning('Failed to generate report.');
                        }
                    };
                    reader.readAsText(blob);
                    return;
                }

                if (contentDisposition) {
                    var matches = contentDisposition.match(/filename="?([^";]+)"?/);
                    if (matches && matches[1]) filename = matches[1];
                }

                if (contentType.includes('application/pdf')) {
                    if (!filename.toLowerCase().endsWith('.pdf')) filename += '.pdf';
                } else if (contentType.includes('spreadsheetml') || contentType.includes('excel')) {
                    if (!filename.toLowerCase().endsWith('.xlsx')) filename += '.xlsx';
                } else {
                    if (!filename.toLowerCase().endsWith('.pdf')) filename += '.pdf';
                }

                var blobUrl = window.URL.createObjectURL(blob);
                var a = document.createElement('a');
                a.href = blobUrl;
                a.download = filename;
                document.body.appendChild(a);
                a.click();
                document.body.removeChild(a);
                setTimeout(function () { window.URL.revokeObjectURL(blobUrl); }, 1000);

                toastr.success('Report downloaded successfully!');
            },
            error: function () {
                $('#loadingSpinner1').hide();
                toastr.error('Error generating report.');
            }
        });
    });

});
