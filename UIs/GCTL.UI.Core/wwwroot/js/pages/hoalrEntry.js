let selectedEmpId = null;
let flatpickrInstance;
let currentState = 'checkedout'; // Track current state: 'checkedout' or 'checkedin'

$(document).ready(function () {
    setupLoadingOverlay();
    loadCurrentUser();
    initializeFlatpickr();
    initializeEventHandlers();
    setTimeout(() => {
        loadTodayAttendanceStatus(); // Check today's attendance status on load
        loadAttendanceGrid(); // Load attendance table data
    }, 0)
});

function initializeEventHandlers() {
    $("#btnCheckin-time").on('click', function () {
        var action = "checkin";
        handleFormSubmission(action);
    });

    $("#btnCheckout-time").on('click', function () {
        var action = "checkout";
        handleFormSubmission(action);
    });
}

function handleFormSubmission(action) {
    if (!action) return;
    const checkinBtn = $("#btnCheckin-time");
    const checkoutBtn = $("#btnCheckout-time");
    //debugger;
    const now = new Date();
    const dateValue = new Date($("#Date").val());
    const empId = $("#employeeSelect").val();

    if (!empId) {
        showNotification("Please Select an Employee", "warning");
        return;
    }

    if (now.toDateString() !== dateValue.toDateString()) {
        showNotification("Selected Date is not Today", "warning");
        return;
    }

    if (currentState == 'complete') {
        checkinBtn.prop('disabled', true);
        checkoutBtn.prop('disabled', true);
        showNotification('Attendance complete for today', 'success');
        return;
    }
    // Get local time components (Bangladesh time)
    const year = now.getFullYear();
    const month = String(now.getMonth() + 1).padStart(2, '0');
    const day = String(now.getDate()).padStart(2, '0');
    const hours = String(now.getHours()).padStart(2, '0');
    const minutes = String(now.getMinutes()).padStart(2, '0');
    const seconds = String(now.getSeconds()).padStart(2, '0');

    //// Format: "2026-01-10T18:25:53" (Bangladesh local time)
    const localDateTime = `${year}-${month}-${day}T${hours}:${minutes}:${seconds}`;

    const dataToSend = {
        FingerPrintId: empId,
        Date: dateValue,
        Time: localDateTime,
    };

    console.log('Sending (Bangladesh Time):', JSON.stringify(dataToSend));

    $.ajax({
        url: `/HomeOfficeAttendanceLogRegister/SaveEntry`,
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(dataToSend),
        beforeSend: showLoading,
        success: function (res) {
            if (res.success) {
                showNotification(res.message, 'success');
                loadTodayAttendanceStatus();

                // Reload table data
                loadAttendanceGrid();
            } else {
                showNotification(res.message, 'error');
            }
        },
        error: function (xhr, status, error) {
            console.error("Error saving entry:", error);
            showNotification('Failed to save entry', 'error');
        },
        complete: hideLoading
    });
}

// Function to toggle button states
function toggleButtons(state) {
    currentState = state;
    const checkinBtn = $("#btnCheckin-time");
    const checkoutBtn = $("#btnCheckout-time");

    switch (state) {
        case 'no_hor': // No approved Home Office Request for today
            checkinBtn.prop('disabled', true);
            checkoutBtn.prop('disabled', true);
            //showNotification('No approved Home Office Request for today', 'warning');
            break;

        case 'checkedout': // Ready to check in
            checkinBtn.prop('disabled', false);
            checkoutBtn.prop('disabled', true);
            break;

        case 'checkedin': // Ready to check out
            checkinBtn.prop('disabled', true);
            checkoutBtn.prop('disabled', false);
            break;

        case 'complete': // Both check-in and check-out done
            checkinBtn.prop('disabled', true);
            checkoutBtn.prop('disabled', true);
            // Optionally show a message
            showNotification('Attendance complete for today', 'success');
            break;

        default: // Fallback if state is unknown
            checkinBtn.prop('disabled', true);
            checkoutBtn.prop('disabled', true);
            showNotification('Invalid state', 'warning');
            break;
    }

    //if (state === 'checkedout') {
    //    // Enable Check-In, Disable Check-Out
    //    $("#btnCheckin-time").prop('disabled', false);
    //    $("#btnCheckout-time").prop('disabled', true);
    //} else if (state === 'checkedin') {
    //    // Disable Check-In, Enable Check-Out
    //    $("#btnCheckin-time").prop('disabled', true);
    //    $("#btnCheckout-time").prop('disabled', false);
    //} else {
    //    $("#btnCheckin-time").prop('disabled', false);
    //    $("#btnCheckout-time").prop('disabled', false);
    //}
}

// Function to load today's attendance status
function loadTodayAttendanceStatus() {
    const employeeId = $("#employeeSelect").val();

    if (!employeeId) {
        console.warn("No employee selected");
        toggleButtons('checkedout'); // Default state
        return;
    }

    const today = new Date();
    const year = today.getFullYear();
    const month = String(today.getMonth() + 1).padStart(2, '0');
    const day = String(today.getDate()).padStart(2, '0');
    const dateString = `${year}-${month}-${day}`;

    $.ajax({
        url: '/HomeOfficeAttendanceLogRegister/GetTodayStatus',
        type: 'GET',
        data: {
            employeeId: employeeId,
            date: dateString
        },
        success: function (res) {

            if (res.success && res.data) {
                if (res.data.hasHORApproved === false) {
                    toggleButtons('no_hor');
                } else if (!res.data.hasCheckin) {
                    toggleButtons('checkedout'); // Ready to check in
                } else if (res.data.hasCheckin && !res.data.hasCheckout) {
                    toggleButtons('checkedin');
                } else if (res.data.hasCheckin && res.data.hasCheckout) {
                    toggleButtons('complete');
                }
            } else {
                toggleButtons('checkedout');
            }
        },
        error: function (xhr, status, error) {
            console.error("Error loading attendance status:", error);
            // Default to checked out on error
            toggleButtons('checkedout');
        }
    });
}

// Function to load attendance grid data (Today only)
let attendanceTable = null;

function loadAttendanceGrid() {
    const employeeId = $("#employeeSelect").val();
    const dateValue = $("#Date").val(); // Get selected date
    if (!employeeId) {
        console.error("No employee selected");
        return;
    }
    $.ajax({
        url: '/HomeOfficeAttendanceLogRegister/GetAttendanceData',
        type: 'GET',
        data: {
            empId: employeeId,
            date: dateValue // Send the selected date
        },
        beforeSend: showLoading,
        success: function (res) {
            if (res.success && res.data) {
                populateAttendanceGrid(res.data);
            } else {
                populateAttendanceGrid([]);
            }
        },
        error: function (xhr, status, error) {
            console.error("Error loading attendance data:", error);
            populateAttendanceGrid([]);
        },
        complete: hideLoading
    });
}

// Function to populate attendance grid using DataTables
function populateAttendanceGrid(data) {
    // Destroy existing DataTable if it exists
    if (attendanceTable !== null) {
        attendanceTable.destroy();
    }

    // Clear the table body
    const tbody = $("#HOALR-grid-body");
    tbody.empty();

    // Prepare data for DataTable
    const tableData = data.map(function (record) {
        return [
            record.date ? formatDate(record.date) : '-',
            record.inTime ? formatTime(record.inTime) : '-',
            record.outTime ? formatTime(record.outTime) : '-',
            record.workHours ? record.workHours : '-'
        ];
    });

    // Initialize DataTable
    attendanceTable = $("#HOALR-grid").DataTable({
        data: tableData,
        columns: [
            { title: "Date", className: "text-center" },
            { title: "In Time", className: "text-center" },
            { title: "Out Time", className: "text-center" },
            { title: "Work Hours", className: "text-center" }
        ],
        paging: true,
        pageLength: 10,
        lengthMenu: [[10, 25, 50, -1], [10, 25, 50, "All"]],
        searching: true,
        ordering: true,
        order: [[0, 'desc']], // Sort by date descending by default
        info: true,
        responsive: true,
        language: {
            emptyTable: "No attendance records found",
            zeroRecords: "No matching records found",
            search: "Search:",
            lengthMenu: "Show _MENU_ entries",
            info: "Showing _START_ to _END_ of _TOTAL_ entries",
            infoEmpty: "Showing 0 to 0 of 0 entries",
            infoFiltered: "(filtered from _MAX_ total entries)",
            paginate: {
                first: "First",
                last: "Last",
                next: "Next",
                previous: "Previous"
            }
        }
    });
}
// Format date as DD/MM/YYYY
function formatDate(dateString) {
    const date = new Date(dateString);
    const day = String(date.getDate()).padStart(2, '0');
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const year = date.getFullYear();
    return `${day}/${month}/${year}`;
}

// Format time as HH:MM AM/PM
function formatTime(timeString) {
    const time = new Date(timeString);
    let hours = time.getHours();
    const minutes = String(time.getMinutes()).padStart(2, '0');
    const ampm = hours >= 12 ? 'PM' : 'AM';
    hours = hours % 12 || 12;
    return `${hours}:${minutes} ${ampm}`;
}

function setupLoadingOverlay() {
    console.log("Loading");
    if ($("#loadingOverlay").length === 0) {
        $("body").append(`
            <div id="loadingOverlay" style="
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
}

function showLoading() {
    $('body').css('overflow', 'hidden');
    $("#loadingOverlay").fadeIn(200);
}

function hideLoading() {
    $('body').css('overflow', '');
    $("#loadingOverlay").fadeOut(200);
}

function initializeFlatpickr() {

    if (typeof flatpickr === 'undefined') return;

    flatpickrInstance = flatpickr("#Date", {
        dateFormat: "Y-m-d",
        altInput: true,
        altFormat: "d/m/Y",
        allowInput: false,
        clickOpens: false,
        defaultDate: new Date()
    })
}

function loadCurrentUser() {
    const user = $('#employeeSelect').val();

    $.ajax({
        url: '/HomeOfficeAttendanceLogRegister/GetCurrentUser',
        type: 'GET',
        contentType: 'application/json',
        success: function (res) {
            console.log("Current Logged In User: ", res);
            if (res.success == true && res.data == user) {
                populateCurrentUserInfo(res.data);
            } else {
                console.error("Failed To Load Current User Data");
            }
        }, error: function (xhr, status, error) {
            console.error("Error loading current user: ", error);
        }
    })
}

function populateCurrentUserInfo(id) {
    if (!id || id.trim() === '' || id == null) {
        clearEmployeeInfo();
        return;
    }
    $.ajax({
        url: `/HomeOfficeAttendanceLogRegister/GetEmployeeData`,
        type: "GET",
        contentType: "application/json",
        data: { id: id },
        success: function (res) {
            if (res == null) return;
            $(".EmployeeName").text(res.employeeName);
            $(".EmployeeDepartment").text(res.department);
            $(".EmployeeDesignation").text(res.designation);
            $(".EmployeeJoinDate").text(res.joiningDate);
        }
    })
}

function showNotification(message, type) {
    if (typeof toastr !== 'undefined' && toastr[type]) {
        toastr[type](message, type === 'success' ? 'Success' : type === 'error' ? 'Error' : 'Warning');
    } else {
        alert(message);
    }
}









//let selectedEmpId = null;
//let flatpickrInstance;
//let currentState = 'checkedout';

//$(document).ready(function () {
//    setupLoadingOverlay();
//    loadCurrentUser();
//    initializeFlatpickr();
//    initializeEventHandlers();
//    //setTimeout(() => {
//        loadTodayAttendanceStatus();
//        loadAttendanceGrid();
//    //}, 500);
//});

//function initializeEventHandlers() {
//    $("#btnCheckin-time").on('click', function () {
//        var action = "checkin";
//        handleFormSubmission(action);
//    });

//    $("#btnCheckout-time").on('click', function () {
//        var action = "checkout";
//        handleFormSubmission(action);
//    });
//}

//function handleFormSubmission(action) {
//    if (!action) return;

//    const now = new Date();
//    const dateValue = $("#Date").val();

//    // Format as ISO 8601 string for reliable parsing
//    const localDateTime = now.toISOString();

//    const dataToSend = {
//        FingerPrintId: $("#employeeSelect").val(),
//        Date: dateValue,  // Make sure this is also in ISO format or "yyyy-MM-dd"
//        Time: localDateTime
//    };

//    console.log('Sending (Bangladesh Time):', JSON.stringify(dataToSend));

//    console.log(JSON.stringify(dataToSend));

//    $.ajax({
//        url: `/HomeOfficeAttendanceLogRegister/SaveEntry`,
//        type: 'POST',
//        contentType: 'application/json',
//        data: JSON.stringify(dataToSend),
//        beforeSend: showLoading,
//        success: function (res) {
//            if (res.success) {
//                //clearForm();
//                showNotification(res.message, 'success');

//                if (action === 'checkin') {
//                    toggleButtons('checkedin');
//                } else if (action === 'checkout') {
//                    toggleButtons('checkedout');
//                }

//                loadAttendanceGrid();
//                //loadTableData();
//            } else {
//                showNotification(res.message, 'error');
//            }
//        },
//        error: function (xhr, status, error) {
//            console.error("Error loading current user:", error);
//        },
//        complete: hideLoading
//    });
//}

//function setupLoadingOverlay() {
//    console.log("Loading");
//    if ($("#loadingOverlay").length === 0) {
//        $("body").append(`
//            <div id="loadingOverlay" style="
//                display: none;
//                position: fixed;
//                top: 0;
//                left: 0;
//                width: 100%;
//                height: 100%;
//                background-color: rgba(0, 0, 0, 0.5);
//                z-index: 9999;
//                justify-content: center;
//                align-items: center;">
//                <div style="
//                    background-color: white;
//                    padding: 20px;
//                    border-radius: 5px;
//                    box-shadow: 0 0 10px rgba(0,0,0,0.3);
//                    text-align: center;">
//                    <div class="spinner-border text-primary" role="status">
//                    </div>
//                </div>
//            </div>
//        `);
//    }
//}

//function showLoading() {
//    $('body').css('overflow', 'hidden');
//    $("#loadingOverlay").fadeIn(200);
//}

//function hideLoading() {
//    $('body').css('overflow', '');
//    $("#loadingOverlay").fadeOut(200);
//}

//function initializeFlatpickr() {

//    if (typeof flatpickr === 'undefined') return;

//    flatpickrInstance = flatpickr("#Date", {
//        dateFormat: "Y-m-d",
//        altInput: true,
//        altFormat: "d/m/Y",
//        allowInput: false,
//        clickOpens: false,
//        defaultDate: new Date()
//    })
//}

//function loadCurrentUser() {
//    const user = $('#employeeSelect').val();

//    $.ajax({
//        url: '/HomeOfficeAttendanceLogRegister/GetCurrentUser',
//        type: 'GET',
//        contentType: 'application/json',
//        success: function (res) {
//            //debugger;
//            console.log("Current Logged In User: ", res);
//            if (res.success == true && res.data == user) {
//                populateCurrentUserInfo(res.data);
//            } else {
//                console.error("Failed To Load Current User Data");
//            }
//        }, error: function (xhr, status, error) {
//            console.error("Error loading current user: ", error);
//        }
//    })
//}

//function populateCurrentUserInfo(id) {
//    if (!id || id.trim() === '' || id == null) {
//        clearEmployeeInfo();
//        return;
//    }
//    $.ajax({
//        url: `/HomeOfficeAttendanceLogRegister/GetEmployeeData`,
//        type: "GET",
//        contentType: "application/json",
//        data: { id: id },
//        success: function (res) {
//            if (res == null) return;
//            $(".EmployeeName").text(res.employeeName);
//            $(".EmployeeDepartment").text(res.department);
//            $(".EmployeeDesignation").text(res.designation);
//            $(".EmployeeJoinDate").text(res.joiningDate);
//        }
//    })
//}

//function showNotification(message, type) {
//    if (typeof toastr !== 'undefined' && toastr[type]) {
//        toastr[type](message, type === 'success' ? 'Success' : type === 'error' ? 'Error' : 'Warning');
//    } else {
//        alert(message);
//    }
//}

//// Function to toggle button states
//function toggleButtons(state) {
//    currentState = state;

//    if (state === 'checkedout') {
//        // Enable Check-In, Disable Check-Out
//        $("#btnCheckin-time").prop('disabled', false);
//        $("#btnCheckout-time").prop('disabled', true);
//    } else if (state === 'checkedin') {
//        // Disable Check-In, Enable Check-Out
//        $("#btnCheckin-time").prop('disabled', true);
//        $("#btnCheckout-time").prop('disabled', false);
//    }
//}

//// Function to load today's attendance status
//function loadTodayAttendanceStatus() {
//    const employeeId = $("#employeeSelect").val();
//    const today = new Date();
//    const year = today.getFullYear();
//    const month = String(today.getMonth() + 1).padStart(2, '0');
//    const day = String(today.getDate()).padStart(2, '0');
//    const dateString = `${year}-${month}-${day}`;

//    $.ajax({
//        url: '/HomeOfficeAttendanceLogRegister/GetTodayStatus',
//        type: 'GET',
//        data: {
//            employeeId: employeeId,
//            date: dateString
//        },
//        success: function (res) {
//            //debugger;
//            if (res.success && res.data) {
//                // If there's check-in but no check-out, user is checked in
//                if (res.data.hasCheckin && !res.data.hasCheckout) {
//                    toggleButtons('checkedin');
//                } else {
//                    toggleButtons('checkedout');
//                }
//            } else {
//                // No records for today, default to checked out
//                toggleButtons('checkedout');
//            }
//        },
//        error: function (xhr, status, error) {
//            console.error("Error loading attendance status:", error);
//            // Default to checked out on error
//            toggleButtons('checkedout');
//        }
//    });
//}


//// Function to load attendance grid data
//function loadAttendanceGrid() {
//    const employeeId = $("#employeeSelect").val();
//    const dateValue = $("#Date").val(); // Get selected date

//    if (!employeeId) {
//        console.error("No employee selected");
//        return;
//    }

//    $.ajax({
//        url: '/HomeOfficeAttendanceLogRegister/GetAttendanceData',
//        type: 'GET',
//        data: {
//            empId: employeeId,
//            date: dateValue // Send the selected date
//        },
//        beforeSend: showLoading,
//        success: function (res) {
//            if (res.success && res.data) {
//                populateAttendanceGrid(res.data);
//            } else {
//                $("#HOALR-grid-body").html(`
//                    <tr>
//                        <td colspan="4" class="text-center text-muted">No attendance records found for today</td>
//                    </tr>
//                `);
//            }
//        },
//        error: function (xhr, status, error) {
//            console.error("Error loading attendance data:", error);
//            $("#HOALR-grid-body").html(`
//                <tr>
//                    <td colspan="4" class="text-center text-danger">Failed to load attendance data</td>
//                </tr>
//            `);
//        },
//        complete: hideLoading
//    });
//}

//// Function to populate attendance grid
//function populateAttendanceGrid(data) {

//    const tbody = $("#HOALR-grid-body");
//    tbody.empty();

//    if (!data || data.length === 0) {
//        tbody.html(`
//            <tr>
//                <td colspan="4" class="text-center text-muted">No attendance records found</td>
//            </tr>
//        `);
//        return;
//    }

//    data.forEach(function (record) {
//        const inTime = record.inTime ? formatTime(record.inTime) : '-';
//        const outTime = record.outTime ? formatTime(record.outTime) : '-';
//        const workHours = record.workHours ? record.workHours : '-';
//        const date = record.date ? formatDate(record.date) : '-';

//        const row = `
//            <tr>
//                <td class="text-center">${date}</td>
//                <td class="text-center">${inTime}</td>
//                <td class="text-center">${outTime}</td>
//                <td class="text-center">${workHours}</td>
//            </tr>
//        `;

//        tbody.append(row);
//    });
//}

//// Format date as DD/MM/YYYY
//function formatDate(dateString) {
//    const date = new Date(dateString);
//    const day = String(date.getDate()).padStart(2, '0');
//    const month = String(date.getMonth() + 1).padStart(2, '0');
//    const year = date.getFullYear();
//    return `${day}/${month}/${year}`;
//}

//// Format time as HH:MM AM/PM
//function formatTime(timeString) {
//    const time = new Date(timeString);
//    let hours = time.getHours();
//    const minutes = String(time.getMinutes()).padStart(2, '0');
//    const ampm = hours >= 12 ? 'PM' : 'AM';
//    hours = hours % 12 || 12;
//    return `${hours}:${minutes} ${ampm}`;
//}