CREATE DATABASE IF NOT EXISTS typing_battle;
CREATE USER IF NOT EXISTS 'typing'@'localhost' IDENTIFIED BY 'typing_dev';
GRANT ALL PRIVILEGES ON typing_battle.* TO 'typing'@'localhost';
SHOW DATABASES LIKE 'typing_battle';
