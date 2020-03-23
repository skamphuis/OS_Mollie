$(document).ready(function () {
    $('#os_mollie_cmdSave').unbind("click");
    $('#os_mollie_cmdSave').click(function () {
        $('.processing').show();
        $('.actionbuttonwrapper').hide();
        // lower case cmd must match ajax provider ref.
        nbxget('os_mollie_savesettings', '.OS_Molliedata', '.OS_Molliereturnmsg');
    });

    $('.selectlang').unbind("click");
    $(".selectlang").click(function () {
        $('.editlanguage').hide();
        $('.actionbuttonwrapper').hide();
        $('.processing').show();
        $("#nextlang").val($(this).attr("editlang"));
        // lower case cmd must match ajax provider ref.
        nbxget('os_mollie_selectlang', '.OS_Molliedata', '.OS_Molliedata');
    });

    $(document).on("nbxgetcompleted", os_mollie_nbxgetCompleted); // assign a completed event for the ajax calls

    // function to do actions after an ajax call has been made.
    function os_mollie_nbxgetCompleted(e) {
        $('.processing').hide();
        $('.actionbuttonwrapper').show();
        $('.editlanguage').show();

        if (e.cmd == 'os_mollie_selectlang') {
                        
        }
    };
});

