var x: integer;

begin
  x := 1;
  repeat
    writeln(x);
    if (x = 3) then break;
    x += 1;
  until x = 5;
end.