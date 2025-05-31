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

garbage_sql_files = glob.glob('/home/ubuntu/Maroik/*.sql')
for garbage_sql_file in garbage_sql_files:
    os.remove(garbage_sql_file)

current_date = str(datetime.today().strftime("%Y%m%d"))
backup_sql_file_name = f'Init-{current_date}.sql'
os.system(f'sudo docker exec -i Maroik.PostgreSQL /bin/bash -c "PGPASSWORD=qwer12\!@# pg_dump --username postgres \"Maroik\"" > /home/ubuntu/Maroik/{backup_sql_file_name}')

msg = MIMEMultipart()

msg['From'] = 'example@example.com'
msg['To'] = 'example@example.com'
msg['Date'] = formatdate(localtime=True)
msg['Subject'] = Header(s=f'Maroik PostgreSQL SQL Backup ({current_date})', charset='utf-8')
body = MIMEText(f'Maroik PostgreSQL SQL Backup ({current_date})', _charset='utf-8')
msg.attach(body)

files = list()
files.append(os.path.expanduser(f'/home/ubuntu/Maroik/{backup_sql_file_name}'))

for f in files:
    part = MIMEBase('application', "octet-stream")
    part.set_payload(open(f, "rb").read())
    encode_base64(part)
    part.add_header('Content-Disposition', 'attachment; filename="%s"' % os.path.basename(f))
    msg.attach(part)

mailServer = smtplib.SMTP_SSL('smtp.host.com')
mailServer.login('exmaple@exmaple.com', 'password')
mailServer.send_message(msg)
mailServer.quit()

EOF
