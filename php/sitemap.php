<?php
require('Httpful/autoload.php');
require('libphp-phpmailer/autoload.php');

use PHPMailer\PHPMailer\PHPMailer;
use PHPMailer\PHPMailer\SMTP;
use PHPMailer\PHPMailer\Exception;

function download ($lang) {
    header ('Content-Type: text/xml');  
    header ('Content-Disposition: attachment');

    echo create ($lang);
}

function send ($lang, $recipients) {
    $username = '?';
    $password = '?';

    $mail = new PHPMailer();

    //Server settings
    $mail->SMTPDebug = SMTP::DEBUG_SERVER;                      // Enable verbose debug output
    $mail->isSMTP();                                            // Send using SMTP
    $mail->Host       = 'smtp.gmail.com';                    // Set the SMTP server to send through
    $mail->SMTPAuth   = true;                                   // Enable SMTP authentication
    $mail->Username   = $username;                     // SMTP username
    $mail->Password   = $password;                               // SMTP password
    $mail->SMTPSecure = PHPMailer::ENCRYPTION_STARTTLS;         // Enable TLS encryption; `PHPMailer::ENCRYPTION_SMTPS` encouraged
    $mail->Port       = 587;                                    // TCP port to connect to, use 465 for `PHPMailer::ENCRYPTION_SMTPS` above

    //Recipients
    $mail->setFrom('carlo.marchiori@gmail.com', 'Carlo Marchiori');
    foreach ($recipients as $recipient) {
        $mail->addAddress($recipient);     // Add a recipient
    }

    $sitemap = create ($lang);
    $temp = tmpfile();
    fwrite($temp, $sitemap);
    fseek($temp, 0);
    $mail->addAttachment(stream_get_meta_data($temp)['uri']);         // Add attachments
    fclose($temp); // this removes the file    

    // Content
    $mail->isHTML(true);                                  // Set email format to HTML
    $mail->Subject = 'MUSEMENT.COM sitemap for ' . $lang;
    $mail->Body    = 'Please find the sitemap as <b>attachment</b>.';
    $mail->AltBody = 'Please find the sitemap as attachment.';

    $mail->send();
    echo 'Message has been sent';
}

function create ($lang) {
    $limit = 20;
    $baseurl = 'https://api.musement.com/api/v3/';
    $cityPriority = '0.7';
    $activityPriority = '0.5';
    
    $url = $baseurl . 'cities?limit=' . $limit;
    error_log ('Querying cityes ' . $url);
    $citiesResponse = \Httpful\Request::get($url)
        ->addHeader ('Accept-Language', $lang)
        ->send ();
    
    $xw = xmlwriter_open_memory();
    xmlwriter_set_indent($xw, 1);
    $res = xmlwriter_set_indent_string($xw, ' ');
    
    xmlwriter_start_document($xw, '1.0', 'UTF-8');
    xmlwriter_start_element($xw, 'urlset');
    
    xmlwriter_start_attribute($xw, 'xmlns');
    xmlwriter_text($xw, 'http://www.sitemaps.org/schemas/sitemap/0.9');
    xmlwriter_end_attribute($xw);
    
    foreach ($citiesResponse->body as $city) {
    
        xmlwriter_start_element($xw, 'url');
    
        xmlwriter_start_element($xw, 'loc');
        xmlwriter_text($xw, $city->url);
        xmlwriter_end_element($xw);
    
        xmlwriter_start_element($xw, 'priority');
        xmlwriter_text($xw, $cityPriority);
        xmlwriter_end_element($xw);
    
        xmlwriter_end_element($xw); 
    
        $url = $baseurl . 'cities/' . $city->id . '/activities?limit=' . $limit;
        error_log ('Querying activities for city ' . $city->name . ', url ' . $url);
        $response = \Httpful\Request::get($url)
            ->addHeader ('Accept-Language', $lang)
            ->send ();
            
        error_log ('Response type ' . gettype ($response));
        
        foreach ($response->body->data as $activity) {
            xmlwriter_start_element($xw, 'url');
    
            xmlwriter_start_element($xw, 'loc');
            xmlwriter_text($xw, $activity->url);
            xmlwriter_end_element($xw);
        
            xmlwriter_start_element($xw, 'priority');
            xmlwriter_text($xw, $activityPriority);
            xmlwriter_end_element($xw);
        
            xmlwriter_end_element($xw);
        }
    }    
    
    xmlwriter_end_element($xw); 
    xmlwriter_end_document($xw);
    
    return xmlwriter_output_memory($xw);
}

$action = $_POST ['action'];
$lang = $_POST ['lang'];
$recipients = $_POST ['recipients'];

error_log ('Action is ' . $action);

if ($action == 'download') {
    download ($lang);
} elseif ($action == 'send') {
    send ($lang, $recipients);
}
?>