#! /bin/bash

python3 - << 'EOF'

import glob
import os
import smtplib
from email.encoders import encode_base64
from email.header import Header
from email.mime.base import MIMEBase
from email.mime.multipart import MIMEMultipart
from email.mime.text import MIMEText
from email.utils import formatdate
from datetime import datetime

garbage_tar_xz_files = glob.glob('/home/ubuntu/Maroik/*.tar.xz')
for garbage_tar_xz_file in garbage_tar_xz_files:
    os.remove(garbage_tar_xz_file)

current_date = str(datetime.today().strftime("%Y%m%d"))
website_backup_upload_file_name = f'website-upload-{current_date}'
os.system(f'sudo docker cp Maroik.WebSite:/app/wwwroot/upload /home/ubuntu/Maroik/{website_backup_upload_file_name}')
os.system(f'sudo tar cfJ /home/ubuntu/Maroik/{website_backup_upload_file_name}.tar.xz /home/ubuntu/Maroik/{website_backup_upload_file_name}')
os.system(f'sudo rm -rf /home/ubuntu/Maroik/{website_backup_upload_file_name}')

file_storage_backup_upload_file_name = f'file-storage-upload-{current_date}'
os.system(f'sudo docker cp Maroik.FileStorage:/app/upload /home/ubuntu/Maroik/{file_storage_backup_upload_file_name}')
os.system(f'sudo tar cfJ /home/ubuntu/Maroik/{file_storage_backup_upload_file_name}.tar.xz /home/ubuntu/Maroik/{file_storage_backup_upload_file_name}')
os.system(f'sudo rm -rf /home/ubuntu/Maroik/{file_storage_backup_upload_file_name}')

msg = MIMEMultipart()

msg['From'] = 'example@example.com'
msg['To'] = 'example@example.com'
msg['Date'] = formatdate(localtime=True)
msg['Subject'] = Header(s=f'Maroik.WebSite Upload File Backup ({current_date})', charset='utf-8')
body = MIMEText(f'Maroik.WebSite Upload File Backup ({current_date})', _charset='utf-8')
msg.attach(body)

files = list()
files.append(f'/home/ubuntu/Maroik/{website_backup_upload_file_name}.tar.xz')
files.append(f'/home/ubuntu/Maroik/{file_storage_backup_upload_file_name}.tar.xz')

for f in files:
    part = MIMEBase('application', "octet-stream")
    part.set_payload(open(f, "rb").read())
    encode_base64(part)
    part.add_header('Content-Disposition', 'attachment; filename="%s"' % os.path.basename(f))
    msg.attach(part)

mailServer = smtplib.SMTP_SSL('smtp.host.com')
mailServer.login('example@example.com', 'password')
mailServer.send_message(msg)
mailServer.quit()

EOF
