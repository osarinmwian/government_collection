-- Create UserOTP table for 2FA validation in Government Collections database
CREATE TABLE UserOTP (
    id int IDENTITY(1,1) PRIMARY KEY,
    username nvarchar(200) NULL,
    email nvarchar(200) NULL,
    mobilenumber nvarchar(200) NULL,
    datecreated datetime2 NOT NULL DEFAULT GETUTCDATE(),
    datevalidated datetime2 NULL,
    source nvarchar(50) NULL,
    action nvarchar(100) NULL,
    RequestID nvarchar(100) NULL,
    RequestType nvarchar(50) NULL,
    tokencode nvarchar(max) NULL,
    tokenExpiryDate datetime2 NOT NULL,
    OTPStatus nvarchar(50) NULL,
    OTPPurpose nvarchar(50) NULL,
    AccountNo nvarchar(50) NULL,
    DebitAccount nvarchar(50) NULL
);

-- Create indexes for better performance
CREATE INDEX IX_UserOTP_Username ON UserOTP(username);
CREATE INDEX IX_UserOTP_RequestID ON UserOTP(RequestID);
CREATE INDEX IX_UserOTP_TokenCode ON UserOTP(tokencode);
CREATE INDEX IX_UserOTP_DateValidated ON UserOTP(datevalidated);