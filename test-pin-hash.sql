-- Query to check the actual stored PIN hash for Adaugotest
SELECT 
    username,
    transactionpin,
    bvn,
    LEN(transactionpin) as pin_length,
    pinstatus
FROM OmniProfiles 
WHERE username = 'Adaugotest';