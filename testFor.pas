var i: integer;

function Gen: sequence of integer;
begin
  for var i := 10 downto 1 do
    yield i;
  for var i := 2 to 5 do
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