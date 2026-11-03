-- The student's real name, for the teacher's pages only; display_name becomes the public nickname (v0.20).
-- Names typed before v0.20 were real names, so each student keeps theirs; the teacher then sets a nickname.
ALTER TABLE users ADD COLUMN real_name TEXT NOT NULL DEFAULT '';
UPDATE users SET real_name = display_name WHERE role = 'student';
