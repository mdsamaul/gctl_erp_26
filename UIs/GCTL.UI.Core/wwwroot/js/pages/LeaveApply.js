
var gridState = {
    page: 1,
    pageSize: 10,
    search: '',
    sortColumn: '',
    sortDir: 'desc',
    empId: ''
};


$(document).ready(function () {


    //#region Date

    // Date pickers
    getDateFromFlatPicker("#halfDate");
    getDateFromFlatPicker("#fromDate");
    getDateFromFlatPicker("#toDate");

    getDateFromFlatPicker("#shortDate");

    function getDateFromFlatPicker(id) {
        flatpickr(id, CalendarService.createConfig({ defaultDate: new Date() }));
    }

    // Inline time pickers
    initTimePicker('fromTime', new Date(), false);
    initTimePicker('toTime', new Date(), false);

    function initTimePicker(inputId, defaultTime, disableTime) {
        const input = document.getElementById(inputId);
        const hiddenInput = document.getElementById(inputId + 'Hidden');
        if (!input) return;

        if (input._flatpickr) {
            try { input._flatpickr.destroy(); } catch (e) { }
            input._flatpickr = undefined;
        }

        const fp = flatpickr(input, {
            enableTime: true,
            noCalendar: true,
            dateFormat: "h:i:s K",
            time_24hr: false,
            enableSeconds: true,
            inline: true,
            defaultDate: defaultTime || new Date(),
            minuteIncrement: 1,
            onChange: function (selectedDates) {
                if (selectedDates.length > 0 && hiddenInput) {
                    hiddenInput.value = selectedDates[0].toLocaleTimeString('en-US', {
                        hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: true
                    });
                }
                // Recalculate hours whenever either time picker changes
                var fromHidden = $("#fromTimeHidden").val();
                var toHidden = $("#toTimeHidden").val();
                if (fromHidden && toHidden) {
                    var from = new Date(`1970-01-01 ${fromHidden}`);
                    var to = new Date(`1970-01-01 ${toHidden}`);
                    var diff = (to - from) / (1000 * 60 * 60);
                    $("#hoursBetween").val(diff >= 0 ? diff.toFixed(2) : "0");
                }
            }
        });

        input.disabled = disableTime;
        input.readOnly = disableTime;
        if (fp.calendarContainer) {
            fp.calendarContainer.classList.toggle('fp-ui-disabled', disableTime);
        }
    }

    //#endregion

    //initializeDataTable();
    fetchEntryId();

    poulateLeaveTypeDropdown();
    function poulateLeaveTypeDropdown() {
        $.ajax({
            url: '/LeaveApplicationEntry/GetLeaveTypes', // adjust controller name
            type: 'GET',
            success: function (data) {
                var dropdown = $('#leaveTypeDropdown');
                dropdown.empty();
                dropdown.append('<option value="">--Select--</option>');
                $.each(data, function (i, item) {
                    dropdown.append('<option value="' + item.leaveTypeCode + '">' + item.name + '</option>');
                });
            },
            error: function () {
                alert('Failed to load leave types.');
            }
        });
    }

    $("#fromDate, #toDate").change(function () {
        validateDates();

    });

    //$("#fromTime, #toTime").change(function () {
    //    validateTimes();
    //});

    var selectedEmpId = $('#employeeDropdown').val();
    getEmployeeLeaveBalance(selectedEmpId);
    getEmployeeLeavePending(selectedEmpId);

    initializeLeaveApplicationsTable(selectedEmpId)


    function initializeLeaveApplicationsTable(empId) {
        gridState.empId = empId || '';
        gridState.page = 1;
        loadGrid();
    }

    function loadGrid() {
        var $tbody = $('#leaveApplicationsTable tbody');
        $tbody.html('<tr><td colspan="13" class="text-center"><i class="fas fa-spinner fa-spin"></i> Loading...</td></tr>');

        $.ajax({
            url: '/LeaveApplicationEntry/Grid',
            type: 'GET',
            data: {
                page: gridState.page,
                pageSize: gridState.pageSize,
                searchValue: gridState.search,
                sortColumn: gridState.sortColumn,
                sortDir: gridState.sortDir,
                empId: gridState.empId
            },
            success: function (response) {
                renderGridRows(response.data);
                renderPagination(response.totalRecords, response.totalPages, response.currentPage, response.pageSize);
            },
            error: function () {
                $tbody.html('<tr><td colspan="13" class="text-center text-danger">Failed to load data.</td></tr>');
            }
        });
    }

    function renderGridRows(data) {
        var $tbody = $('#leaveApplicationsTable tbody');
        $tbody.empty();

        if (!data || data.length === 0) {
            $tbody.html('<tr><td colspan="13" class="text-center">No leave applications found.</td></tr>');
            return;
        }

        $.each(data, function (i, row) {
            var actionBtns =
                '<button onclick="editLeaveApplication(\'' + row.leaveAppEntryCode + '\')" class="btn btn-primary btn-sm mb-1"><i class="fas fa-edit"></i></button> ' +
                '<button onclick="deleteLeaveApplication(\'' + row.leaveAppEntryCode + '\')" class="btn btn-danger btn-sm"><i class="fas fa-trash"></i></button>';

            var fileBtnHtml = (!row.sickLeaveFilePath)
                ? '<span>No file</span>'
                : '<button onclick="downloadLeaveApplication(\'' + row.sickLeaveFilePath + '\')" class="btn btn-info btn-sm"><i class="fas fa-download"></i></button>';

            var startDate = row.startDate ? new Date(row.startDate).toLocaleDateString('en-GB') : '';
            var endDate = row.endDate ? new Date(row.endDate).toLocaleDateString('en-GB') : '';

            var daysHtml = '';
            if (Array.isArray(row.days) && row.days.length > 0) {
                daysHtml = '<ul style="list-style-type:none;margin:0;padding:0;">' +
                    row.days.map(function (d) { return '<li>' + d.split('T')[0] + '</li>'; }).join('') +
                    '</ul>';
            }

            var approvalHtml = formatApprovalStatus(row.hodApprovalStatus);

            var tr = '<tr>' +
                '<td class="text-center" style="width:5%;">' + actionBtns + '</td>' +
                '<td class="text-center">' + (row.leaveAppEntryId || '') + '</td>' +
                '<td class="text-center">' + (row.applyLeaveFormat || '') + '</td>' +
                '<td class="text-center">' + approvalHtml + '</td>' +
                '<td class="text-center">' + (row.employeeID || '') + '</td>' +
                '<td class="text-start">' + (row.employeeFirstName || '') + '</td>' +
                '<td class="text-center">' + (row.leaveTypeId || '') + '</td>' +
                '<td class="text-center">' + fileBtnHtml + '</td>' +
                '<td class="text-center">' + startDate + '</td>' +
                '<td class="text-center">' + endDate + '</td>' +
                '<td class="text-center">' + daysHtml + '</td>' +
                '<td class="text-center">' + (row.noOfDay || '') + '</td>' +
                '<td class="text-start">' + (row.reason || '') + '</td>' +
                '</tr>';
            $tbody.append(tr);
        });
    }

    function renderPagination(totalRecords, totalPages, currentPage, pageSize) {
        var $wrap = $('#gridPaginationWrap');
        var $showres = $('#showres');
        $wrap.empty();
        $showres.empty();

        if (totalPages <= 0) return;

        var from = (currentPage - 1) * pageSize + 1;
        var to = Math.min(currentPage * pageSize, totalRecords);
        $wrap.append('<span class="me-3 text-muted small">Showing ' + from + '–' + to + ' of ' + totalRecords + '</span>');

        var pageSizeHtml = '<select id="gridPageSize" class="form-control form-control-sm d-inline-block me-3" style="width:auto;">';
        [10, 25, 50, 100].forEach(function (n) {
            pageSizeHtml += '<option value="' + n + '"' + (n === pageSize ? ' selected' : '') + '>' + n + '</option>';
        });
        pageSizeHtml += '</select>';
        $showres.append(pageSizeHtml);

        var ul = $('<ul class="pagination pagination-sm mb-0 d-inline-flex"></ul>');
        ul.append('<li class="page-item' + (currentPage === 1 ? ' disabled' : '') + '">' +
            '<a class="page-link grid-page-btn" data-page="' + (currentPage - 1) + '" href="#">&laquo;</a></li>');

        var startPage = Math.max(1, currentPage - 2);
        var endPage = Math.min(totalPages, currentPage + 2);
        for (var p = startPage; p <= endPage; p++) {
            ul.append('<li class="page-item' + (p === currentPage ? ' active' : '') + '">' +
                '<a class="page-link grid-page-btn" data-page="' + p + '" href="#">' + p + '</a></li>');
        }
        ul.append('<li class="page-item' + (currentPage === totalPages ? ' disabled' : '') + '">' +
            '<a class="page-link grid-page-btn" data-page="' + (currentPage + 1) + '" href="#">&raquo;</a></li>');

        $wrap.append(ul);
    }

    // Pagination click
    $(document).on('click', '.grid-page-btn', function (e) {
        e.preventDefault();
        var p = parseInt($(this).data('page'));
        if (p >= 1) { gridState.page = p; loadGrid(); }
    });

    // Page-size change
    $(document).on('change', '#gridPageSize', function () {
        gridState.pageSize = parseInt($(this).val());
        gridState.page = 1;
        loadGrid();
    });

    // Search
    var gridSearchTimer;
    $(document).on('input', '#gridSearchInput', function () {
        clearTimeout(gridSearchTimer);
        var val = $(this).val();
        gridSearchTimer = setTimeout(function () {
            gridState.search = val;
            gridState.page = 1;
            loadGrid();
        }, 400);
    });

    // Sort
    $(document).on('click', '#leaveApplicationsTable thead th[data-col]', function () {
        var col = $(this).data('col');
        gridState.sortColumn = col;
        gridState.sortDir = (gridState.sortColumn === col && gridState.sortDir === 'asc') ? 'desc' : 'asc';
        gridState.page = 1;
        $('#leaveApplicationsTable thead th[data-col]').removeClass('sort-asc sort-desc');
        $(this).addClass('sort-' + gridState.sortDir);
        loadGrid();
    });


    

    function getEmployeeLeaveBalance(empId) {
        console.log('getEmployeeLeaveBalance Id', empId)
        if (empId) {
            $.ajax({
                url: '/LeaveApplicationEntry/GetEmployeeLeaveBalance',
                type: 'GET',
                data: { EmpId: empId },
                success: function (response) {
                    if (response.success) {
                        console.log('LeaveStatus Table', response);
                        LeaveBalance(response.data);
                    } else {
                        toastr.warning('Failed to fetch employee RestInfo details.', 'Message');
                    }
                },
                error: function () {
                    toastr.warning('Error fetching employee details.', 'Message');
                }
            });
        } else {
            clearEmployeeFields();
        }
    }

    function getEmployeeLeavePending(empId) {
        console.log('getEmployeeLeavePending Id', empId)
        if (empId) {
            $.ajax({
                url: '/LeaveApplicationEntry/GetEmployeeLeaveStatusList',
                type: 'GET',
                data: { EmpId: selectedEmpId },
                success: function (response) {
                    console.log('getEmployeeLeavePending', response)
                    if (response.success) {
                        console.log('getEmployeeLeavePending', response)
                        EmployeeLeaveStatus(response.data)

                    } else {
                        // alert('Failed to fetch employee RestInfo details.');
                        toastr.warning('Failed to fetch employee RestInfo details.', 'Message');

                    }
                },
                error: function () {
                    toastr.warning('Error fetching employee details.', 'Message');

                    //alert('Error fetching employee details.');
                }
            });
        }
    }



    // Approved leave

    //#region Leave Approved

    // ── Leave Taken (Pending) table state ───────────────────────────────
    var leaveTakenState = {
        allData: [],
        page: 1,
        pageSize: 10,
        search: '',
        sortColumn: '',
        sortDir: 'asc'
    };

    function EmployeeLeaveStatus(data) {
        leaveTakenState.allData = data || [];
        leaveTakenState.page = 1;
        leaveTakenState.search = '';
        $('#leaveTakenSearch').val('');
        $('#leaveTakenTable thead th[data-col]').removeClass('sort-asc sort-desc');
        renderLeaveTakenTable();
    }

    function getFilteredSortedLeaveTaken() {
        var rows = leaveTakenState.allData.slice();

        if (leaveTakenState.search) {
            var term = leaveTakenState.search.toLowerCase();
            rows = rows.filter(function (r) {
                var startStr = r.startDate ? new Date(r.startDate).toLocaleDateString('en-GB') : '';
                var endStr = r.endDate ? new Date(r.endDate).toLocaleDateString('en-GB') : '';
                return (String(r.leaveAppEntryId || '').toLowerCase().includes(term)) ||
                    (startStr.toLowerCase().includes(term)) ||
                    (endStr.toLowerCase().includes(term)) ||
                    (String(r.noOfDay || '').toLowerCase().includes(term)) ||
                    (String(r.reason || '').toLowerCase().includes(term));
            });
        }

        if (leaveTakenState.sortColumn) {
            var col = leaveTakenState.sortColumn;
            var dir = leaveTakenState.sortDir === 'asc' ? 1 : -1;
            rows.sort(function (a, b) {
                var va = a[col], vb = b[col];
                if (col === 'startDate' || col === 'endDate') {
                    va = va ? new Date(va).getTime() : 0;
                    vb = vb ? new Date(vb).getTime() : 0;
                } else if (col === 'noOfDay') {
                    va = parseFloat(va) || 0;
                    vb = parseFloat(vb) || 0;
                } else {
                    va = String(va || '').toLowerCase();
                    vb = String(vb || '').toLowerCase();
                }
                if (va < vb) return -1 * dir;
                if (va > vb) return 1 * dir;
                return 0;
            });
        }

        return rows;
    }

    function renderLeaveTakenTable() {
        var $tbody = $('#leaveTakenTbody');
        $tbody.empty();

        var filtered = getFilteredSortedLeaveTaken();
        var totalRecords = filtered.length;
        var totalPages = Math.max(1, Math.ceil(totalRecords / leaveTakenState.pageSize));
        if (leaveTakenState.page > totalPages) leaveTakenState.page = totalPages;

        var start = (leaveTakenState.page - 1) * leaveTakenState.pageSize;
        var pageRows = filtered.slice(start, start + leaveTakenState.pageSize);

        if (pageRows.length === 0) {
            $tbody.html('<tr><td colspan="6" class="text-center">No records found.</td></tr>');
        } else {
            $.each(pageRows, function (i, row) {
                var startStr = row.startDate ? new Date(row.startDate).toLocaleDateString('en-GB') : '';
                var endStr = row.endDate ? new Date(row.endDate).toLocaleDateString('en-GB') : '';

                var daysHtml = '';
                if (Array.isArray(row.days)) {
                    daysHtml = row.days.map(d => new Date(d).toLocaleDateString('en-GB')).join(', ');
                } else if (row.days) {
                    daysHtml = new Date(row.days).toLocaleDateString('en-GB');
                }

                var tr = '<tr>' +
                    '<td style="color: blue;">' + (row.leaveAppEntryId || '') + '</td>' +
                    '<td>' + startStr + '</td>' +
                    '<td>' + endStr + '</td>' +
                    '<td>' + daysHtml + '</td>' +
                    '<td>' + (row.noOfDay || 0) + '</td>' +
                    '<td class="resn-cell">' + (row.reason || '') + '</td>' +
                    '</tr>';
                $tbody.append(tr);
            });
        }

        var totalDays = filtered.reduce((sum, r) => sum + (parseFloat(r.noOfDay) || 0), 0);
        $('#leaveTakenTotalFooter').text(totalDays);

        var from = totalRecords === 0 ? 0 : start + 1;
        var to = Math.min(start + leaveTakenState.pageSize, totalRecords);
        $('#leaveTakenInfo').text('Showing ' + from + ' to ' + to + ' of ' + totalRecords + ' results');

        renderLeaveTakenPagination(totalRecords, totalPages, leaveTakenState.page);
    }

    function renderLeaveTakenPagination(totalRecords, totalPages, currentPage) {
        var $wrap = $('#leaveTakenPaginationWrap');
        $wrap.empty();
        if (totalRecords === 0) return;

        // var pageSizeHtml = '<select id="leaveTakenPageSize" class="form-control form-control-sm d-inline-block" style="width:auto;">';
        // [10, 25, 50].forEach(function (n) {
        //     pageSizeHtml += '<option value="' + n + '"' + (n === leaveTakenState.pageSize ? ' selected' : '') + '>' + n + '</option>';
        // });
        // pageSizeHtml += '</select>';
        // $wrap.append(pageSizeHtml);

        var ul = $('<ul class="pagination pagination-sm mb-0"></ul>');
        ul.append('<li class="page-item' + (currentPage === 1 ? ' disabled' : '') + '">' +
            '<a class="page-link leave-taken-page-btn" data-page="' + (currentPage - 1) + '" href="#">&laquo;</a></li>');

        var startPage = Math.max(1, currentPage - 2);
        var endPage = Math.min(totalPages, currentPage + 2);
        for (var p = startPage; p <= endPage; p++) {
            ul.append('<li class="page-item' + (p === currentPage ? ' active' : '') + '">' +
                '<a class="page-link leave-taken-page-btn" data-page="' + p + '" href="#">' + p + '</a></li>');
        }

        ul.append('<li class="page-item' + (currentPage === totalPages ? ' disabled' : '') + '">' +
            '<a class="page-link leave-taken-page-btn" data-page="' + (currentPage + 1) + '" href="#">&raquo;</a></li>');

        $wrap.append(ul);
    }

    $(document).on('change', '#leaveTakenPageSize', function () {
        leaveTakenState.pageSize = parseInt($(this).val());
        leaveTakenState.page = 1;
        renderLeaveTakenTable();
    });

    $(document).on('click', '.leave-taken-page-btn', function (e) {
        e.preventDefault();
        var p = parseInt($(this).data('page'));
        if (p >= 1) { leaveTakenState.page = p; renderLeaveTakenTable(); }
    });

    $(document).on('change', '#leaveTakenPageSize', function () {
        leaveTakenState.pageSize = parseInt($(this).val());
        leaveTakenState.page = 1;
        renderLeaveTakenTable();
    });

    var leaveTakenSearchTimer;
    $(document).on('input', '#leaveTakenSearch', function () {
        clearTimeout(leaveTakenSearchTimer);
        var val = $(this).val();
        leaveTakenSearchTimer = setTimeout(function () {
            leaveTakenState.search = val;
            leaveTakenState.page = 1;
            renderLeaveTakenTable();
        }, 300);
    });

    $(document).on('click', '#leaveTakenTable thead th[data-col]', function () {
        var col = $(this).data('col');
        if (leaveTakenState.sortColumn === col) {
            leaveTakenState.sortDir = leaveTakenState.sortDir === 'asc' ? 'desc' : 'asc';
        } else {
            leaveTakenState.sortColumn = col;
            leaveTakenState.sortDir = 'asc';
        }
        leaveTakenState.page = 1;
        $('#leaveTakenTable thead th[data-col]').removeClass('sort-asc sort-desc');
        $(this).addClass('sort-' + leaveTakenState.sortDir);
        renderLeaveTakenTable();
    });

    //#endregion

    



    function LeaveBalance(data) {
        console.log('LeaveBalance Func:', data);
        $('#leaveAvailableTable').DataTable({
            destroy: true,
            data: data,
            columns: [
                {
                    data: 'shortName',
                    title: 'LN',
                    width: '15%',
                    className: 'dt-custom-padding'
                },
                {
                    data: 'availableLeave',
                    title: 'YLE',
                    width: '20%',
                    className: 'dt-custom-padding'
                },
                {
                    data: 'totalLeaveTaken',
                    title: 'TAK',
                    width: '20%',
                    className: 'dt-custom-padding'
                },
                {
                    data: 'remainingLeave',
                    title: 'BAL',
                    width: '20%',
                    className: 'dt-custom-padding'
                },
                {
                    data: 'totalShortLeaveTimeHour',
                    title: 'Sh.L',
                    width: '15%',
                    className: 'dt-custom-padding',
                    render: function (data) {
                        return data !== null && data !== undefined ? data : '';
                    }
                }
            ],
            scrollY: '400px',
            scrollCollapse: true,
            autoWidth: true,
            responsive: true,
            searching: false,
            paging: false,
            info: false,
            ordering: false
        });
    }


    const isEditMode = window.location.pathname.toLowerCase().includes('edit');
    const leaveAppEntryCode = new URLSearchParams(window.location.search).get('id');

    if (isEditMode && leaveAppEntryCode) {
        loadLeaveApplication(leaveAppEntryCode);
    }

    // Load existing leave application data
    function loadLeaveApplication(id) {
        $.ajax({
            url: '/LeaveApplicationEntry/GetLeaveApplication1',
            type: 'GET',
            data: { id: id },
            success: function (response) {
                if (response.success) {
                    populateForm(response.data);
                } else {
                    toastr.warning('Error fetching employee details.', 'Message');

                    // alert('Failed to load leave application.');
                }
            },
            error: function () {
                toastr.warning('Error fetching employee details.', 'Message');

                //alert('Error loading leave application.');
            }
        });
    }

    //----------------- Populate form with existing data

    function populateForm(data) {
        $('#leaveAppEntryCode').val(data.leaveAppEntryCode);
        $('#entryId').val(data.leaveAppEntryId);
        $('#leaveTypeDropdown').val(data.leaveTypeId);
        $('textarea[name="Reason"]').val(data.reason);

        // map DB value → dropdown value
        var formatMap = { 'FullLeave': 'fullDayLeave', 'HalfDayLeave': 'halfDayLeave', 'ShortLeave': 'shortLeave' };
        var leaveFormatVal = formatMap[data.applyLeaveFormat] || data.applyLeaveFormat;
        $('#applyLeaveFormat').val(leaveFormatVal).trigger('change');

        switch (leaveFormatVal) {
            case 'fullDayLeave':
                if (data.startDate && $("#fromDate")[0]._flatpickr) {
                    $("#fromDate")[0]._flatpickr.setDate(data.startDate.split('T')[0]);
                }
                if (data.endDate && $("#toDate")[0]._flatpickr) {
                    $("#toDate")[0]._flatpickr.setDate(data.endDate.split('T')[0]);
                }
                $('#daysBetween').val(data.noOfDay);

                calendar.getEvents().forEach(e => e.remove());
                selectedDates = [];
                if (data.days && data.days.length > 0) {
                    data.days.forEach(date => {
                        var fd = date.split('T')[0];
                        selectedDates.push(fd);
                        calendar.addEvent({ title: 'Selected', start: fd, allDay: true, backgroundColor: '#28a745', borderColor: '#28a745' });
                    });
                    calendar.gotoDate(selectedDates[0]);
                }
                $('#calendarContainer').show();
                setTimeout(() => { calendar.render(); calendar.updateSize(); }, 100);
                break;

            case 'halfDayLeave':
                $('#halfDayCheckbox').prop('checked', data.halfDay === "1");
                if (data.firstOrSecondHalf === "1") $('#firstHalf').prop('checked', true);
                else if (data.firstOrSecondHalf === "2") $('#secondHalf').prop('checked', true);
                if (data.startDate && $("#halfDate")[0]._flatpickr) {
                    $("#halfDate")[0]._flatpickr.setDate(data.startDate.split('T')[0]);
                }
                break;

            case 'shortLeave':
                if (data.shortLeaveFrom && $("#fromTime")[0]._flatpickr) {
                    $("#fromTime")[0]._flatpickr.setDate(new Date(data.shortLeaveFrom));
                }
                if (data.shortLeaveTo && $("#toTime")[0]._flatpickr) {
                    $("#toTime")[0]._flatpickr.setDate(new Date(data.shortLeaveTo));
                }
                if (data.startDate && $("#shortDate")[0]._flatpickr) {
                    $("#shortDate")[0]._flatpickr.setDate(data.startDate.split('T')[0]);
                }
                break;
        }

        if (data.sickLeaveFilePath) {
            $('#leaveFile').next('.mt-2').remove();
            $('#leaveFile').after(`<div class="mt-2"><small>Current file: ${data.sickLeaveFilePath.split('/').pop()}</small></div>`);
        }
    }

    

    //------------------------------------------

    function validateTimes() {
        var fromTime = $("#fromTime").val();
        var toTime = $("#toTime").val();

        if (fromTime && toTime) {
            // Convert times to Date objects for comparison
            var from = new Date("1970-01-01T" + fromTime + "Z");
            var to = new Date("1970-01-01T" + toTime + "Z");

            if (from > to) {
                toastr.warning("The 'From' time must be earlier than the 'To' time.", 'Message');

                // alert("The 'From' time must be earlier than the 'To' time.");
                $("#fromTime").get(0).setCustomValidity("From time must be before To time.");
                $("#toTime").get(0).setCustomValidity("");
                $('#toTime').val('');
            } else {
                $("#fromTime").get(0).setCustomValidity("");
                $("#toTime").get(0).setCustomValidity("");
            }
        }
    }

    //----------------------------

    function validateDates() {
        var fromDate = $("#fromDate").val();
        var toDate = $("#toDate").val();

        if (fromDate && toDate) {
            var from = new Date(fromDate);
            var to = new Date(toDate);

            if (from > to) {
                toastr.warning("The 'From' date must be greater than the 'To' date.", 'Message');

                //alert("The 'From' date must be greater than the 'To' date.");
                $("#fromDate").get(0).setCustomValidity("From date must be before To date.");
                $("#toDate").get(0).setCustomValidity("");
                $('#toDate').val('');
            } else {
                $("#fromDate").get(0).setCustomValidity("");
                $("#toDate").get(0).setCustomValidity("");
            }
        }
    }


    //-----------------------------


    function fetchEntryId() {
        $.ajax({
            url: '/LeaveApplicationEntry/GenerateEntryId', // Replace with your actual controller name
            type: 'GET',
            success: function (response) {
                // Set the entryId to the input field
                $('#entryId').val(response.entryId.result);
                console.log('new entry Id : ', response)
            },
            error: function (xhr, status, error) {
                console.error('Error fetching entryId:', error);
            }
        });
    }








    window.downloadLeaveApplication = function (id) {
        // Show a confirmation dialog
        //   var confirmation = confirm('Are you sure you want to download this leave application?');

        // If the user clicks "OK", proceed with the download
        // if (confirmation) {
        console.log('Download leave application with ID:', id);

        // Perform an AJAX GET request to fetch data or perform an action
        $.ajax({
            url: `/LeaveApplicationEntry/DownloadLeaveApplication`,
            type: 'GET',
            data: { url: id },
            success: function (response) {
                // Handle the successful response
                console.log('Response:', response);

                // Assuming the response contains a URL to download the file
                // You can create a link and trigger a download
                const downloadUrl = response.downloadUrl; // Adjust based on your response structure
                if (downloadUrl) {
                    window.open(downloadUrl, '_blank'); // Redirect to the download URL
                } else {
                    toastr.warning('Download link not available.', 'Message');

                    //alert('Download link not available.');
                }
            },
            error: function (xhr, status, error) {
                // Handle the error response
                console.error('Error:', error);
                toastr.warning('Failed to download leave application. Please try again.', 'Message');

                //alert('Failed to download leave application. Please try again.');
            }
        });
        // } else {
        //      // User clicked "Cancel", do nothing
        //      console.log('Download canceled by user.');
        //  }
    };







    // Define the function globally
    window.deleteLeaveApplication = function (id) {
        // Show a confirmation dialog
        var confirmation = confirm('Are you sure you want to delete this leave application?');

        // If the user clicks "OK", proceed with the deletion
        if (confirmation) {
            console.log('Deleting leave application with ID:', id);

            // Perform an AJAX GET request to fetch data or perform an action
            $.ajax({
                url: `/LeaveApplicationEntry/DeleteLeaveApplication`,
                type: 'GET',
                data: { id: id },
                success: function (response) {
                    // Handle the successful response
                    console.log('Response: delete', response);
                    toastr.success(response.message, 'Success');
                    // alert('Leave application deleted successfully.');
                    //window.location.href = `/LeaveApplicationEntry/Create`
                    //reloadDataTable();
                    getEmployeeLeaveBalance(selectedEmpId);
                    getEmployeeLeavePending(selectedEmpId);
                    initializeLeaveApplicationsTable(selectedEmpId);
                    fetchEntryId();


                },
                error: function (xhr, status, error) {
                    // Handle the error response
                    console.error('Error:', error);
                    // alert('Failed to delete leave application. Please try again.');
                    toastr.warning('Failed to delete leave application. Please try again.', 'Message');
                }
            });
        } else {
            // User clicked "Cancel", do nothing
            console.log('Deletion canceled by user.');
        }
    };







    // Define the function globally
    window.editLeaveApplication = function (id) {
        console.log('Editing leave application with ID:', id);

        // Perform an AJAX GET request to fetch data or perform an action
        $.ajax({
            url: `/LeaveApplicationEntry/GetLeaveApplication1`,
            type: 'GET',
            data: { id: id },
            success: function (response) {


                // Handle the successful response
                console.log('Response:', response);


                populateForm(response.data);


            },
            error: function (xhr, status, error) {
                // Handle the error response
                console.error('Error:', error);
                toastr.warning('Failed to fetch leave application details. Please try again.', 'Message');

                //alert('Failed to fetch leave application details. Please try again.');
            }
        });
    };



    function formatApprovalStatus(status) {
        if (!status) return '';

        const statusClasses = {
            'Approved': 'text-success',
            'Pending': 'text-warning',
            'Rejected': 'text-danger'
        };

        const className = statusClasses[status] || 'text-secondary';
        return `<span class="${className}">${status}</span>`;
    }





    //------------------------

    // Initialize - hide all format specific inputs
    $("#fullDayInputs, #halfDayInputs, #oneDayInputs").hide();  /* , #calendarContainer */






    // Function to clear employee fields
    function clearEmployeeFields() {
        $('#employeeName').val('');
        $('#designation').val('');
        $('#department').val('');
        $('#immediateSupervisor').empty().append('<option value="">--Select Supervisor--</option>');
        $('#headOfDepartment').empty().append('<option value="">--Select HOD--</option>');


        

    }

    // Leave Format Change Event
    $("#applyLeaveFormat").change(function () {
        const selectedFormat = $(this).val();

        // Hide all sections first
        $("#fullDayInputs, #halfDayInputs, #oneDayInputs").hide();  /*  , #calendarContainer */

        // Show relevant section based on selection
        switch (selectedFormat) {
            case "fullDayLeave":
                $("#fullDayInputs, #calendarContainer").show();
                calendar.render();
                setTimeout(() => calendar.updateSize(), 100);
                break;
            case "halfDayLeave":
                $("#halfDayInputs").show();
                break;
            case "shortLeave":
                $("#oneDayInputs").show();
                break;
        }
    });

    // Full Day Leave Date Calculations
    $("#fromDate, #toDate").change(function () {
        const fromDate = new Date($("#fromDate").val());
        const toDate = new Date($("#toDate").val());

        if (fromDate && toDate && !isNaN(fromDate) && !isNaN(toDate)) {
            updateDateRange(fromDate, toDate);
        }
    });

    // One Day Leave Time Calculations
    $("#fromTime, #toTime").change(function () {
        const fromTime = $("#fromTime").val();
        const toTime = $("#toTime").val();

        if (fromTime && toTime) {
            const from = new Date(`1970-01-01T${fromTime}`);
            const to = new Date(`1970-01-01T${toTime}`);
            const difference = (to - from) / (1000 * 60 * 60);
            $("#hoursBetween").val(difference >= 0 ? difference.toFixed(2) : "0");
        }
    });

    // Half Day Leave Controls
    $("#halfDayCheckbox").change(function () {
        if ($(this).is(":checked")) {
            $("input[name='halfDayType']").prop('disabled', false);
        } else {
            $("input[name='halfDayType']").prop('disabled', true).prop('checked', false);
        }
    });

    // Calendar Setup
    // Calendar Setup
    let selectedDates = [];
    var calendarEl = document.getElementById('calendar');

    const calendar = new FullCalendar.Calendar(calendarEl, {
        height: 435,
        initialView: 'dayGridMonth',
        headerToolbar: {
            left: 'prev,next today',
            center: 'title',
            right: 'dayGridMonth'
        },
        selectable: true,
        unselectAuto: false,
        select: function (info) {
            const date = info.startStr;
            // First, check if we already have this date
            const existingEvent = calendar.getEvents().find(event =>
                event.startStr.split('T')[0] === date
            );

            if (existingEvent) {
                // If date exists, remove it
                existingEvent.remove();
                selectedDates = selectedDates.filter(d => d !== date);
            } else {
                // If date doesn't exist, add it
                selectedDates.push(date);
                calendar.addEvent({
                    id: date, // Add an ID to make it easier to find events
                    title: 'Selected',
                    start: date,
                    allDay: true,
                    backgroundColor: '#28a745',
                    borderColor: '#28a745'
                });
            }

            calendar.unselect();
            updateDayCount();
            updateDateInputs();
        },
        eventClick: function (info) {
            const date = info.event.startStr.split('T')[0];
            selectedDates = selectedDates.filter(d => d !== date);
            info.event.remove();
            updateDayCount();
            updateDateInputs();
        },
        // Ensure consistent date formatting
        eventDidMount: function (info) {
            if (!info.event.id) {
                info.event.setExtendedProp('id', info.event.startStr.split('T')[0]);
            }
        }
    });

    // Updated helper function for populating calendar
    function clearAndPopulateCalendar(dates) {
        // Clear existing events
        calendar.removeAllEvents();
        selectedDates = [];

        // Add new dates
        dates.forEach(date => {
            const formattedDate = date.split('T')[0];
            if (!selectedDates.includes(formattedDate)) {
                selectedDates.push(formattedDate);
                calendar.addEvent({
                    id: formattedDate,
                    title: 'Selected',
                    start: formattedDate,
                    allDay: true,
                    backgroundColor: '#28a745',
                    borderColor: '#28a745'
                });
            }
        });

        updateDayCount();
        updateDateInputs();
    }

    // Use this function when populating form for edit
    function populateCalendarDates(data) {
        if (data.days && Array.isArray(data.days)) {
            clearAndPopulateCalendar(data.days);
        }
    }


    // Add CSS
    $('#calendar').css({
        'max-width': '100%',


        'font-size': '0.9em'
    });

    // Optional: Make day cells smaller
    $('#calendar').find('.fc-daygrid-day').css({
        'padding': '1px'
    });


    //-----------------------------

    // Updated Calendar Helper Functions
    function updateDateInputs() {
        if (selectedDates.length > 0) {
            selectedDates.sort();
            $("#fromDate").val(selectedDates[0]);
            $("#toDate").val(selectedDates[selectedDates.length - 1]);
            $("#daysBetween").val(selectedDates.length);      // $("#daysBetween").val(calculateBusinessDays());

            updateDayCount(); // Call updateDayCount when dates are updated
        } else {
            $("#fromDate").val('');
            $("#toDate").val('');
            $("#daysBetween").val('0');
        }
    }

    function calculateBusinessDays() {
        // if (selectedDates.length === 0) return 0;

        // let count = 0;
        // // Count only the specifically selected dates that are business days
        // selectedDates.forEach(dateStr => {
        //     const date = new Date(dateStr);
        //     const dayOfWeek = date.getDay();
        //     if (dayOfWeek !== 0 && dayOfWeek !== 6) {
        //         count++;
        //     }
        // });
        // return count;

        return selectedDates.length;
    }

    function updateDayCount() {
        const businessDays = calculateBusinessDays();
        $("#daysBetween").val(businessDays);
    }

    function updateDateRange(fromDate, toDate) {
        // Clear existing selections
        selectedDates = [];
        calendar.getEvents().forEach(event => event.remove());

        if (fromDate <= toDate) {
            let currentDate = new Date(fromDate);
            while (currentDate <= toDate) {
                const dateStr = currentDate.toISOString().split('T')[0];

                // // Only add business days
                // const dayOfWeek = currentDate.getDay();
                // if (dayOfWeek !== 0 && dayOfWeek !== 6) {

                selectedDates.push(dateStr);
                calendar.addEvent({
                    title: 'Selected',
                    start: dateStr,
                    allDay: true,
                    backgroundColor: '#28a745',
                    borderColor: '#28a745'
                });
                //  }
                currentDate.setDate(currentDate.getDate() + 1);
            }
            updateDayCount();
        }
    }

    // Update the calendar select event handler
    calendar.setOption('select', function (info) {
        const date = info.startStr;


        // const selectedDate = new Date(date);
        //  const dayOfWeek = selectedDate.getDay();



        //-------//-----------//--------------//-----------//--------
        // Only allow selection of business days
        // if (dayOfWeek !== 0 && dayOfWeek !== 6) {
        if (!selectedDates.includes(date)) {
            selectedDates.push(date);
            calendar.addEvent({
                title: 'Selected',
                start: date,
                allDay: true,
                backgroundColor: '#28a745',
                borderColor: '#28a745'
            });
        }
        updateDayCount();
        updateDateInputs();
        // } else {
        //     alert('Please select only business days (Monday to Friday).');
        // }
    });

    //------------------------------------

    // Form Submission
    // Replace the existing form submission with this:
    $("form").on("submit", function (e) {
        e.preventDefault();

        var formData = new FormData();


        $("#leaveSubmitBtn").prop("disabled", true);
        $("#leaveSubmitBtn .btn-text").text("Submitting...");
        $("#leaveSubmitBtn .spinner-border").removeClass("d-none");

        // Add file
        var fileInput = $("#leaveFile")[0].files[0];
        if (fileInput) {
            formData.append("LeaveFile", fileInput);
        }

        // Add other form fields
        formData.append("EntryId", $("#entryId").val());
        formData.append("LeaveAppEntryCode", $("#leaveAppEntryCode").val());
        formData.append("CompanyCode", $("#companyDropdown").val());
        formData.append("EmployeeId", $("#employeeDropdown").val());
        formData.append("ImmediateSupervisor", $("#immediateSupervisor").val());
        formData.append("HeadOfDepartment", $("#headOfDepartment").val());
        formData.append("LeaveFormat", $("#applyLeaveFormat").val());
        formData.append("LeaveType", $("#leaveTypeDropdown").val());
        formData.append("Reason", $("textarea").val());

        // Format-specific data
        const leaveFormat = $("#applyLeaveFormat").val();
        switch (leaveFormat) {
            case "fullDayLeave":
                formData.append("FromDate", $("#fromDate").val());
                formData.append("ToDate", $("#toDate").val());
                formData.append("TotalDays", $("#daysBetween").val());

                selectedDates.forEach((date, index) => {
                    formData.append(`SelectedDates[${index}]`, date);
                });


                // formData.append("SelectedDates", JSON.stringify(selectedDates));
                break;

        

            case "halfDayLeave":
                formData.append("IsHalfDay", $("#halfDayCheckbox").is(":checked"));
                formData.append("IsFirstHalf", $("#firstHalf").is(":checked"));
                formData.append("IsSecondHalf", $("#secondHalf").is(":checked"));
                formData.append("HalfDate", $("#halfDate").val());
                break;

            case "shortLeave":
                formData.append("FromTime", $("#fromTime").val());
                formData.append("ToTime", $("#toTime").val());
                formData.append("TotalHours", $("#hoursBetween").val());
                break;
        }
        console.log('formData submit - ', formData)

        $.ajax({

            url: '/LeaveApplicationEntry/SubmitIndividualLeaveApplication',
            type: 'POST',
            data: formData,
            processData: false,
            contentType: false,
            success: function (response) {

                $("#leaveSubmitBtn").prop("disabled", false);
                $("#leaveSubmitBtn .btn-text").text("Leave Apply");
                $("#leaveSubmitBtn .spinner-border").addClass("d-none");

                if (response.isSuccess) {
                    // alert('  Leave application submitted successfully! ');
                    toastr.success('  Leave application submitted successfully! ', 'Success');
                    resetForm();
                    //reloadDataTable();
                    getEmployeeLeaveBalance(selectedEmpId);
                    getEmployeeLeavePending(selectedEmpId);
                    initializeLeaveApplicationsTable(selectedEmpId);

                } else {
                    toastr.warning(response.message, 'Message');
                    //  alert(' Failed to submit: ' + response.message);

                    //window.location.href = '/LeaveApplicationEntry/Create';
                    console.log(response)
                }
            },
            error: function (xhr, status, error) {

                // Hide loader
                $("#leaveSubmitBtn").prop("disabled", false);
                $("#leaveSubmitBtn .btn-text").text("Leave Apply");
                $("#leaveSubmitBtn .spinner-border").addClass("d-none");

                console.error('Error:', xhr.responseText);
                toastr.warning('Submission failed. Check console for details.', 'Message');
                //alert('Submission failed. Check console for details.');
            }
        });
    });




    function resetForm() {
        // capture what needs to persist
        var companyVal = $('#companyDropdown').val();
        var companyText = $('#companyDropdown option:selected').text();
        var supervisorVal = $('#immediateSupervisor').val();
        var hodVal = $('#headOfDepartment').val();
        var empId = selectedEmpId;
        var empText = $('#employeeDropdown option:selected').text();

        // full reset
        $("form")[0].reset();
        $('textarea[name="Reason"]').val('');
        selectedDates = [];
        calendar.getEvents().forEach(event => event.remove());
        $("#fullDayInputs, #halfDayInputs, #oneDayInputs").hide();
        $('#leaveFile').val('');
        $('input[type="radio"], input[type="checkbox"]').prop('checked', false);
        $('textarea#Reason').val('');
        $('#fromDate').val(''); $('#toDate').val('');
        $('#fromTime').val(''); $('#toTime').val('');
        $('#leaveTypeDropdown, #applyLeaveFormat').val('').trigger('change');
        $('#employeeDropdown').val('').trigger('change');

        // restore persisted values
        if (companyVal) {
            $('#companyDropdown').val(companyVal).trigger('change');
        }

        setTimeout(function () {
            // restore employee
            if (empId) {
                if ($('#employeeDropdown option[value="' + empId + '"]').length === 0) {
                    $('#employeeDropdown').append(new Option(empText, empId, true, true));
                }
                $('#employeeDropdown').val(empId).trigger('change');
            }

            // restore supervisor and HOD
            $('#immediateSupervisor').val(supervisorVal);
            $('#headOfDepartment').val(hodVal);
        }, 500);

        poulateLeaveTypeDropdown();

        // new entry ID
        fetchEntryId();
    }




    
    calendar.render();
});


