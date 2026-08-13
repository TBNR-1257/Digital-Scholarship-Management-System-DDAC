-- Create dedicated application user and grant privileges for ddac_scholarship
-- Run this as a MySQL user with sufficient privileges (e.g., root)

CREATE USER IF NOT EXISTS 'ddac_app'@'127.0.0.1' IDENTIFIED BY 'DDAC_AppUser_Pass!';
CREATE USER IF NOT EXISTS 'ddac_app'@'localhost' IDENTIFIED BY 'DDAC_AppUser_Pass!';

GRANT ALL PRIVILEGES ON ddac_scholarship.* TO 'ddac_app'@'127.0.0.1';
GRANT ALL PRIVILEGES ON ddac_scholarship.* TO 'ddac_app'@'localhost';

FLUSH PRIVILEGES;
-- Note: For production use, grant only necessary privileges and use a stronger password stored securely.
