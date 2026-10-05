(function ($) {
    $.leaveSummaryReport = function (options) {

        const settings = $.extend({
            baseUrl: "/",
            companyIds: "#companySelect",
            branchIds: "#branchSelect",
            departmentIds: "#departmentSelect",
            designationIds: "#designationSelect",
            employeeIds: "#employeeSelect",
            previewContainer: "#leaveSummaryReport-container .card-body",
            load: () => console.log("Loading...")
        }, options);

        const urls = {
            filter: `${settings.baseUrl}/getAllFilterEmp`,
            export: `${settings.baseUrl}/ExportReport`,
            preview: `${settings.baseUrl}/PreviewReport`
        };

        let filterChangeBound = false;

        const toArray = val => val ? (Array.isArray(val) ? val : [val]) : [];
        const showLoading = () => $("#customLoadingOverlay").css("display", "flex");
        const hideLoading = () => $("#customLoadingOverlay").hide();

        const showToast = (type, message) => {
            if (typeof toastr !== 'undefined') {
                toastr[type](message);
            } else {
                alert(message);
            }
        };

        const setupLoadingOverlay = () => {
            if ($("#customLoadingOverlay").length === 0) {
                $("body").append(`
                    <div id="customLoadingOverlay" style="display:none;position:fixed;top:0;left:0;width:100%;height:100%;background:rgba(0,0,0,0.5);z-index:9999;justify-content:center;align-items:center;">
                        <div style="background:white;padding:20px;border-radius:5px;box-shadow:0 0 10px rgba(0,0,0,0.3);text-align:center;">
                            <div class="spinner-border text-primary"></div>
                        </div>
                    </div>
                `);
            }
        };

        const getFilterValue = () => ({
            CompanyCodes: toArray($(settings.companyIds).val()),
            BranchCodes: toArray($(settings.branchIds).val()),
            DepartmentCodes: toArray($(settings.departmentIds).val()),
            DesignationCodes: toArray($(settings.designationIds).val()),
            EmployeeIDs: toArray($(settings.employeeIds).val())
        });

        const populateSelect = (selector, dataList) => {
            const $select = $(selector);
            dataList.forEach(item => {
                if (item.code && item.name && $select.find(`option[value="${item.code}"]`).length === 0) {
                    $select.append(`<option value="${item.code}">${item.name}</option>`);
                }
            });
            $select.multiselect('rebuild');
        };

        const clearDependentDropdowns = selectors => {
            selectors.forEach(selector => {
                $(selector).empty().multiselect('rebuild');
            });
        };

        const bindFilterChangeEvents = () => {
            if (!filterChangeBound) {
                const filterSelectors = [settings.companyIds, settings.branchIds].join(',');
                $(filterSelectors).on("change.loadFilter", function () {
                    loadFilterEmp();
                });
                filterChangeBound = true;
            }
        };

        const loadFilterEmp = () => {
            $.ajax({
                url: urls.filter,
                type: "POST",
                contentType: "application/json",
                data: JSON.stringify(getFilterValue()),
                success: res => {
                    if (!res.isSuccess) {
                        showToast('error', res.message);
                        return;
                    }
                    const { data } = res;
                    if (data.companies?.length)
                        populateSelect(settings.companyIds, data.companies);
                    if (data.branches?.length)
                        populateSelect(settings.branchIds, data.branches);

                    bindFilterChangeEvents();
                },
                error: () => {
                    showToast('error', 'Failed to load data');
                }
            });
        };

        const showPreview = () => {
            const filterData = getFilterValue();

            $("#leaveSummaryReport-container").show();
            showLoading();
            $("#previewIframe").hide();

            $.ajax({
                url: urls.preview,
                type: "POST",
                contentType: "application/json",
                data: JSON.stringify({ FilterData: filterData, ExportFormat: 'pdf' }),
                xhrFields: { responseType: 'blob' },
                success: data => {
                    hideLoading();
                    if (window.currentPdfUrl) {
                        window.URL.revokeObjectURL(window.currentPdfUrl);
                    }

                    const blob = new Blob([data], { type: 'application/pdf' });
                    window.currentPdfUrl = window.URL.createObjectURL(blob);
                    $("#previewIframe").attr('src', window.currentPdfUrl).show();
                },
                error: xhr => {
                    hideLoading();
                    $("#leaveSummaryReport-container").hide();
                    let errorMessage = 'Preview failed. Please try again.';

                    switch (xhr.status) {
                        case 400:
                            errorMessage = 'Invalid request. Please check your inputs.';
                            break;
                        case 404:
                            errorMessage = 'No data found for the selected criteria.';
                            break;
                        default:
                            errorMessage = 'An unexpected error occurred. Please try again later.';
                            break;
                    }
                    showToast('error', errorMessage);
                }
            });
        };

        const hidePreview = () => {
            $("#leaveSummaryReport-container").hide();
            $("#previewIframe").attr('src', '');
            if (window.currentPdfUrl) {
                window.URL.revokeObjectURL(window.currentPdfUrl);
                window.currentPdfUrl = null;
            }
        };

        const exportReport = format => {
            showLoading();

            $.ajax({
                url: urls.export,
                type: "POST",
                contentType: "application/json",
                data: JSON.stringify({ FilterData: getFilterValue(), ExportFormat: format }),
                xhrFields: { responseType: 'blob' },
                success: (data, status, xhr) => {
                    hideLoading();

                    const contentType = xhr.getResponseHeader('Content-Type');
                    const disposition = xhr.getResponseHeader('Content-Disposition');

                    let filename;
                    if (disposition && disposition.indexOf("filename=") != -1) {
                        const match = disposition.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/);
                        if (match && match[1]) {
                            filename = match[1].replace(/['"]/g, '');
                        }
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
                    const blob = new Blob([data], { type: contentType });
                    const link = Object.assign(document.createElement("a"), {
                        href: window.URL.createObjectURL(blob),
                        download: filename
                    });

                    document.body.appendChild(link);
                    link.click();
                    document.body.removeChild(link);
                    window.URL.revokeObjectURL(link.href);
                    clearAllFilters();
                    showToast('success', 'Report exported successfully');
                },
                error: (e) => {
                    hideLoading();
                    let errorMessage = 'Export failed. Please try again.';

                    switch (e.status) {
                        case 400:
                            errorMessage = 'Invalid request. Please check your inputs.';
                            break;
                        case 404:
                            errorMessage = 'No data found for the selected criteria.';
                            break;
                        default:
                            errorMessage = 'An unexpected error occurred. Please try again later.';
                            break;
                    }
                    showToast('error', errorMessage);
                }
            });
        };

        const clearAllFilters = () => {
            [settings.companyIds, settings.branchIds].forEach(selector => {
                $(selector).multiselect('deselectAll', false);
                $(selector).multiselect('updateButtonText');
            });
            ['#departmentSelect', '#designationSelect', '#employeeSelect'].forEach(selector => bsms_Reset(selector));

            hidePreview();
            loadFilterEmp();
        };

        const bindUIEvents = () => {
            $("#previewBtn").on("click", () =>
                $("#leaveSummaryReport-container").is(':visible') ? hidePreview() : showPreview()
            );
            $("#downloadBtn").on("click", () => exportReport($("#exportFormatSelect").val()));
            
            $("#closePreviewBtn").on("click", hidePreview);
            $("#refreshPreviewBtn").on("click", showPreview);
        };

        const init = () => {
            settings.load();
            if (typeof $.fn.multiselect !== 'undefined') {
                bsms_InitializeMultiselects({
                    //'#companySelect': 'Select Company',
                    //'#branchSelect': 'Select Branch'

                     '#companySelect': 'Select Company',
                    '#branchSelect': 'Select Branch',
                    '#departmentSelect': 'Select Department',
                    '#designationSelect': 'Select Designation',
                    '#employeeSelect': 'Select Employee'

                });
            } else {
                console.error('Bootstrap Multiselect not loaded!');
            }

            setupLoadingOverlay();
            bindUIEvents();
            loadFilterEmp();
        };

        init();

        //$(document).ready(function () {
        //    Object.keys(gcCascade).forEach(k => delete gcCascade[k]);
        //    Object.assign(gcCascade, {
        //        "#companySelect": ["#branchSelect", "#departmentSelect", "#designationSelect", "#employeeSelect"],
        //        "#branchSelect": ["#departmentSelect", "#designationSelect", "#employeeSelect"],
        //        "#departmentSelect": ["#designationSelect", "#employeeSelect"],
        //        "#designationSelect": ["#employeeSelect"]
        //    });

        //    gcBindRemoteMultiselect("#departmentSelect", "/GcFilters/department", "Select Department");
        //    gcBindRemoteMultiselect("#designationSelect", "/GcFilters/designation", "Select Designation");
        //    gcBindRemoteMultiselect("#employeeSelect", "/HrmLeaveSummaryReport/employee", "Select Employee");

        //    bsms_InitializeMultiselects({
        //        '#departmentSelect': 'Select Department',
        //        '#designationSelect': 'Select Designation',
        //        '#employeeSelect': 'Select Employee'
        //    });

        //    bsms_BindCascade();
        //});


        $(document).ready(async function () {



            gcBindRemoteMultiselect("#companySelect", "/GcFilters/company", "Select Company");

            gcBindRemoteMultiselect("#branchSelect", "/GcFilters/branch", "Select Branch");

            gcBindRemoteMultiselect("#divisionSelect", "/GcFilters/division", "Select Division");

            gcBindRemoteMultiselect("#departmentSelect", "/GcFilters/department", "Select Department");

            gcBindRemoteMultiselect("#designationSelect", "/GcFilters/designation", "Select Designation");

            gcBindRemoteMultiselect("#employeeSelect", "/GcFilters/employee", "Select Employee");



            gcRegisterSelector("#activityStatusSelect", "employeeStatus");


            bsms_BindCascade();


            bsms_Reset("#companySelect");

            await bsms_LoadNext("#companySelect", "/GcFilters/company");

            await bsms_AutoSelectCompany("001");






            $('#js-leveSummery-report-clear').on('click', function () {
               
              
                $('#departmentSelect').val([]).multiselect('refresh');
                $('#employeeSelect').val([]).multiselect('refresh');
                $('#branchSelect').val([]).multiselect('refresh');
                $('#designationSelect').val([]).multiselect('refresh');
                $('#exportFormatSelect').val('pdf'); // default format reset
            });


            $('.js-leveSummery').on('click', function () {
                // Export button click হলে downloadBtn‑কে trigger করো
                $('#downloadBtn').trigger('click');
            });


            $('.js-HomeOfficeRequest-report-favorite').on('click', function () {
                toastr.success('Favorite button Coming soon!');
            });


          

        });

    };
})(jQuery);