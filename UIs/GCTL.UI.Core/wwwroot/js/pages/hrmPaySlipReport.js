(function ($) {
    $.hrmHomeOfficeRequestReportData = function (options) {

        // const _originalBuildReq = buildReq;
        // buildReq = function (page, search) {
        //     const base = _originalBuildReq(page, search);

        //     if ($("#Date").is(":checked")) {
        //         extra.FromDate = $("#FromDateSelect").val() || null;
        //         extra.ToDate = $("#ToDateSelect").val() || null;
        //         extra.MonthIDs = [];
        //         extra.YearIDs = [];
        //     } else if ($("#Year").is(":checked")) {
        //         extra.FromDate = null;
        //         extra.ToDate = null;
        //         extra.MonthIDs = arrVal("#MonthIds").map(x => parseInt(x, 10));
        //         extra.YearIDs = arrVal("#YearTo").map(x => parseInt(x, 10));
        //     }

        //     return Object.assign(base, extra);
        // };

        var settings = $.extend({
            baseUrl: "/",
            accessCode: null,
            companyIds: "#companySelect",
            branchIds: "#branchSelect",
            departmentIds: "#departmentSelect",
            designationIds: "#designationSelect",
            employeeIds: "#employeeSelect",
            FromDate: "#FromDateSelect",
            MonthIDs: "#MonthIds",
            FlatPicker: ".flatDate",
            ToDate: "#ToDateSelect",
            YearIDs: "#YearTo"
        }, options);

        var filterUrl = settings.baseUrl + "/GetFilters";
        var DownloadUrl = settings.baseUrl + "/GetManualAttendanceData";
        var DownloadExcelUrl = settings.baseUrl + "/ExportAttendanceMovementRegisterExcelAsync";

        var setupLoadingOverlay = function () {
            if ($("#customLoadingOverlay").length === 0) {
                $("body").append(`
                    <div id="customLoadingOverlay" style="
                        display: none;
                        position: fixed;
                        top: 0;
                        left: 0;
                        width: 100%;
                        height: 100%;
                        background-color: rgba(0, 0, 0, 0.5);
                        z-index: 9999;
                        justify-content: center;
                        align-items: center;">
                        <div style="
                            background-color: white;
                            padding: 20px;
                            border-radius: 5px;
                            box-shadow: 0 0 10px rgba(0,0,0,0.3);
                            text-align: center;">
                            <div class="spinner-border text-primary" role="status">

                            </div>
                        </div>
                    </div>
                `);
            }
        };

        function showLoading() {
            $("#customLoadingOverlay").css("display", "flex");
        }

        function hideLoading() {
            $("#customLoadingOverlay").hide();
        }

        function showNotification(message, type = 'info') {
            if (typeof toastr !== 'undefined') {
                const title = { success: 'Success', error: 'Error', warning: 'Warning' }[type] || 'Info';
                toastr[type](message, title);
            } else {

                alert(message);
            }
        }
        function toggleDurationFields() {
            //debugger;
            var now = new Date();
            var monthNumber = now.toLocaleString('en-US', { month: 'long' });
            var year = now.getFullYear();

            if ($("#Date").is(":checked")) {
                $('#FromDateSelect, #ToDateSelect').prop('disabled', false);
                $("#MonthIds, #YearTo").prop('disabled', true);
                if ($("#MonthIds").data("multiselect") || $("#MonthIds").hasClass("multiselect")) {
                    $("#MonthIds").multiselect('disable');
                } else if ($.fn.select2 && $("#MonthIds").data("select2")) {
                    $("#MonthIds").prop('disabled', true).trigger('change.select2');
                }

                if (!$("#MonthIds").val() || $("#MonthIds").val().length === 0) {
                    $("#MonthIds").val(monthNumber).trigger('change');
                }

                GetFlatDate();
            } else if ($("#Year").is(":checked")) {
                $('#FromDateSelect, #ToDateSelect').prop('disabled', true);
                $("#MonthIds, #YearTo").prop('disabled', false);
                if ($("#MonthIds").data("multiselect") || $("#MonthIds").hasClass("multiselect")) {
                    $("#MonthIds").multiselect('enable');
                } else if ($.fn.select2 && $("#MonthIds").data("select2")) {
                    $("#MonthIds").prop('disabled', false).trigger('change.select2');
                }

                if (!$("#MonthIds").val() || $("#MonthIds").val().length === 0) {
                    $("#MonthIds").val(monthNumber).trigger('change');
                }
                $("#YearTo").val(year);

                const dateFromPicker = document.querySelector('#FromDateSelect')?._flatpickr;
                const dateToPicker = document.querySelector('#ToDateSelect')?._flatpickr;
                if (dateFromPicker) dateFromPicker.destroy();
                if (dateToPicker) dateToPicker.destroy();
            }
        }

        $(document).ready(function () {

            toggleDurationFields();

            $("input[name='durationType']").on("change", function () {
                toggleDurationFields();
            });
        });

        var reportDataTable = null;
        var isReadonly = false;

        var GetFlatDate = function () {
            $(settings.FlatPicker).each(function () {
                if (this._flatpickr) {
                    this._flatpickr.destroy();
                }
            });
            flatpickr($(settings.FlatPicker), CalendarService.createConfig({
                defaultDate: 'today'
            }));
        };

        var toArray = function (value) {
            if (!value) return [];
            if (Array.isArray(value)) return value;
            return [value];
        };

        const getFilterValue = function () {
            const fromDateVal = $(settings.FromDate).val();
            const toDateVal = $(settings.ToDate).val();
            const monthVal = $(settings.MonthIDs).val();
            const yearVal = $(settings.YearIDs).val();

            var filterData = {
                CompanyCodes: toArray($(settings.companyIds).val()),
                BranchCodes: toArray($(settings.branchIds).val()),
                DepartmentCodes: toArray($(settings.departmentIds).val()),
                DesignationCodes: toArray($(settings.designationIds).val()),
                EmployeeIDs: toArray($(settings.employeeIds).val())
            };

            if ($("#Date").is(":checked")) {
                filterData.FromDate = fromDateVal
                    ? new Date(fromDateVal).toISOString().split('T')[0]
                    : null;

                filterData.ToDate = toDateVal
                    ? new Date(toDateVal).toISOString().split('T')[0]
                    : null;
            }
            else if ($("#Year").is(":checked")) {
                filterData.MonthIDs = monthVal
                    ? toArray(monthVal).map(x => (typeof x === 'string' && isNaN(parseInt(x, 10))) ? x : parseInt(x, 10))
                    : [];

                filterData.YearIDs = yearVal
                    ? toArray(yearVal).map(x => parseInt(x, 10))
                    : [];
            }

            return filterData;
        }

        const showPreview = () => {
            const filterData = getFilterValue();
            const hasFilters = Object.values(filterData).some(value =>
                (Array.isArray(value) && value.length > 0) ||
                (typeof value === 'string' && value.trim() !== '')
            );

            if (!hasFilters) {
                showNotification('No data found to generate report', 'warning');
                return;
            }
            showLoading();

            $.ajax({
                url: "/HrmPaySlipReport/PreviewReport",
                type: "POST",
                contentType: "application/json",
                data: JSON.stringify({ FilterData: filterData, ExportFormat: 'downloadPdf' }),
                xhrFields: { responseType: 'blob' },
                success: (data, status, xhr) => {
                    hideLoading();
                    if (!data || data.size === 0) {
                        showNotification('No pay slip data found for the given filter.', 'warning');
                        hidePreview();
                        return;
                    }
                    const pdfUrl = URL.createObjectURL(data);

                    const previewContainer = document.getElementById("pdf-preview-container");

                    previewContainer.style.display = "block";
                    previewContainer.innerHTML = "";

                    const iframe = document.createElement("iframe");

                    iframe.style.width = "100%";
                    iframe.style.height = "100%";
                    iframe.style.border = "none";
                    iframe.src = pdfUrl;

                    previewContainer.appendChild(iframe);
                },
                error: function (xhr, status, error) {
                    hideLoading();
                    hidePreview();
                    if (xhr.status === 404) {
                        showNotification('No pay slip data found for the given filter.', 'warning');
                    } else {
                        showNotification('Failed to generate report. Please try again later.', 'error');
                    }
                }
            });
        }

        const hidePreview = () => {
            const previewContainer =
                document.getElementById("pdf-preview-container");
            previewContainer.style.display = "none";
            previewContainer.innerHTML = "";
        };

        const exportReport = format => {
            showLoading();

            $.ajax({
                url: "/HrmPaySlipReport/ExportReport",
                type: "POST",
                contentType: "application/json",
                data: JSON.stringify({ FilterData: getFilterValue(), ExportFormat: format }),
                xhrFields: { responseType: 'blob' },
                success: (data, status, xhr) => {
                    if (!data || data.size === 0) {
                        showNotification('No pay slip data found for the given filter.', 'warning');
                        return;
                    }

                    if (format === "download") {
                        const blob = new Blob([data], { type: 'application/pdf' });
                        const iframe = Object.assign(document.createElement('iframe'), {
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

                    const timestamp = new Date().toISOString().slice(0, 19).replace(/[-T:]/g, '');
                    const extention = { downloadPdf: 'pdf', downloadExcel: 'xlsx', download: 'pdf', default: 'pdf' };
                    const filename = `PaySlipReport_${timestamp}.${extention[format] || extention.default}`;

                    const contentType = xhr.getResponseHeader('Content-Type') || (format === 'downloadExcel' ? 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' : 'application/pdf');
                    const blob = new Blob([data], { type: contentType });
                    const link = Object.assign(document.createElement("a"), {
                        href: window.URL.createObjectURL(blob),
                        download: filename
                    });

                    document.body.appendChild(link);
                    link.click();
                    document.body.removeChild(link);
                    window.URL.revokeObjectURL(link.href);
                    showNotification('Report exported successfully', 'success');
                },
                error: (xhr) => {
                    if (xhr.status === 404) {
                        showNotification('No pay slip data found for the given filter.', 'warning');
                    } else {
                        showNotification('Export failed. Please try again later.', 'error');
                    }
                },
                complete: () => {
                    hideLoading();
                }
            })
        }

        const clearAllFilters = () => {
            const accessControlledSelectors = [
                settings.companyIds,
                settings.branchIds,
                settings.departmentIds,
                settings.designationIds,
                settings.employeeIds
            ];

            const alwaysClearableSelectors = [
                //settings.MonthIDs
            ]

            $("#MonthIds").val('');
            if ($("#MonthIds").data("multiselect")) {
                $("#MonthIds").multiselect('refresh');
            }

            const filterSelectors = isReadonly
                ? alwaysClearableSelectors
                : accessControlledSelectors.concat(alwaysClearableSelectors);

            filterSelectors.forEach(selector => {
                $(selector).multiselect('deselectAll', false);
                $(selector).multiselect('updateButtonText');
            });

            // reset duration type back to default (By Month)
            $("#Year").prop("checked", true);
            toggleDurationFields();

            hidePreview();
        }

        var init = function () {
            setupLoadingOverlay();
            $("#btnPreviewPdf").on("click", () =>
                $("#pdf-preview-container").is(':visible') ? hidePreview() : showPreview()
            );

            $("#downloadReport").on('click', () => exportReport($("#reportText").val()));

            $("#js-PaySlip-report-clear").on("click", () => clearAllFilters());

            //loadFilterEmp()
        }

        init();

        $(document).ready(async function () {
            //bindRemoteMultiselect("#approvalSelect", null, "select Approval Status", null);
            bindRemoteMultiselect("#companySelect", "/GcAccessFilter/companies", "Select Company", "company");
            bindRemoteMultiselect("#branchSelect", "/GcAccessFilter/branches", "Select Branch", "branch");
            bindRemoteMultiselect("#divisionSelect", "/GcAccessFilter/divisions", "Select Division", "division");
            bindRemoteMultiselect("#departmentSelect", "/GcAccessFilter/departments", "Select Department", "department");
            bindRemoteMultiselect("#designationSelect", "/GcAccessFilter/designations", "Select Designation", "designation");
            bindRemoteMultiselect("#employeeSelect", "/GcAccessFilter/employees", "Select Employee", null);
            // ms_InitializeMultiselects({
            //     '#approvalSelect': 'Select Approval Status'
            // });
            isReadonly = settings.accessCode === "0005";

            //ms_InitializeMultiselects();
            //ms_BindCascade();

            if (isReadonly) {
                ms_InitializeMultiselects(null, null, true);
                await ms_ApplyAccessCodeToAll(settings.accessCode);
            } else {
                ms_InitializeMultiselects();
                ms_BindCascade();
                ms_Reset("#companySelect");
                await ms_LoadNext("#companySelect", "...");
                await ms_AutoSelectCompany("001");
                $("#companySelect").trigger('change');
            }
        });

    }
})(jQuery);