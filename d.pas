var x: integer;
begin
  x := 1;  
  while x<5 do
  begin
    write(x, ' ');
    while x < 3 do
      x += 1;
    //x += 2;
    //x += 3;
  end;
end.