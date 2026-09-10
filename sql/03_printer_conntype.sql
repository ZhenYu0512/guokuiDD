/* 迁移脚本：打印机支持有线连接（云打印/有线USB/网口） */
USE GuokuiDD;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Printers') AND name = 'ConnType')
    ALTER TABLE Printers ADD ConnType NVARCHAR(10) NOT NULL DEFAULT 'cloud';
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Printers') AND name = 'Address')
    ALTER TABLE Printers ADD Address NVARCHAR(100) NULL;
GO
