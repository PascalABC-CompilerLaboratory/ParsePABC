function Gen: sequence of integer;
begin
  for var i := 10 downto 1 do
    yield i;
end; 

(* procedure Gen;
begin
  for var i := 10 downto 1 do
    writeln(i);
end; *)

begin
  //Gen();
  var q := Gen();
  foreach var e in q do
    writeln(e);
end.